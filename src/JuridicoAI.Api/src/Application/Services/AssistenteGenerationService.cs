using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace JuridicoAI.Api.Application.Services;

public sealed record AssistenteGenerationContext(string Texto, JsonElement Documento, Guid? ReferenciaId, int? ReferenciaVersao,
    AssistenteHistoryContext? Historico, Guid? AnaliseId, IReadOnlyList<AssistenteFonteRecuperacao>? Fontes, IReadOnlyList<AssistenteAlertaDto>? Alertas, string Evidencias = "", AssistenteCoberturaDocumentalDto? Cobertura = null, string Pedido = "");
public sealed record AssistenteGenerationPlan(string Titulo, IReadOnlyList<AssistenteGenerationSection> Secoes, bool Substituir);
public sealed record AssistenteGenerationSection(string Id, string Titulo, string Objetivo);
public sealed class AssistenteGenerationPlannedException : Exception;

public sealed class AssistenteGenerationService(JuridicoAiDbContext db, IAssistenteWorkspaceRepository repository, IPecaRepository pieces,
    ICasoRepository cases, IAuthContext auth, IAssistenteFeatureAccess features, IAssistenteTransparencyService transparency,
    AssistenteDocumentService documents, IOptionsMonitor<AssistenteIaSettings> options, AssistenteBackgroundIdentity background,
    ICorrelationContext correlation, AssistenteHistoryService? history = null)
{
    public const string Version = "geracao-por-etapas-v1";
    public bool Enabled => options.CurrentValue.DurableGenerationEnabled;
    public static bool Active(string state) => state is "NaFila" or "PreparandoContexto" or "AguardandoDocumentos" or "AguardandoAnalise" or "Executando" or "Redigindo" or "Verificando" or "Publicando";
    public async Task AddAsync(AssistenteConversa conversation, Peca piece, AssistenteMensagemPersistida user, AssistenteMensagemPersistida response,
        CriarSolicitacaoAssistenteRequest request, CancellationToken ct)
    {
        var sources = await documents.CaptureSourcesAsync(conversation.Id, request.AnexoIds, ct);
        var now = DateTime.UtcNow;
        var job = new AssistenteGeracao { Id = Guid.NewGuid(), OrganizationId = auth.OrganizationId, UserId = auth.UserId,
            ConversaId = conversation.Id, PedidoId = user.Id, RespostaId = response.Id, PecaId = piece.Id, ClientRequestId = request.ClientRequestId,
            CorrelationId = correlation.CorrelationId, Modelo = options.CurrentValue.Model, PoliticaVersao = Version,
            FontesIds = AssistentePatchCompiler.Json(sources), CasoSha256 = await CaseHashAsync(piece.CasoId, conversation.Id, request, ct),
            CriadaEm = now, AtualizadaEm = now, PrazoEm = now.AddSeconds(options.CurrentValue.GenerationDeadlineSeconds) };
        await db.SaveChangesAsync(ct);
        db.Add(job); await db.SaveChangesAsync(ct); response.GeracaoId = job.Id; response.Estado = "AguardandoGeracao"; response.ExpiraEm = job.PrazoEm.AddSeconds(30);
    }
    private async Task<string> CaseHashAsync(Guid caseId, Guid conversationId, CriarSolicitacaoAssistenteRequest request, CancellationToken ct)
    {
        AssistentePropostaPersistida? reference = null;
        if (request.PropostaReferenciadaId is { } id)
        {
            reference = await repository.ObterPropostaAsync(id, auth.OrganizationId, auth.UserId, ct) ?? throw Missing();
            if (reference.ConversaId != conversationId) throw Missing();
        }
        return AssistentePatchCompiler.Hash(new { contexto = await RequestContextAsync(caseId, request, ct),
            proposta = reference is null ? null : new { reference.Id, reference.VersaoDecisoes, reference.SubstituidaPorId } });
    }
    public async Task<string> RequestContextAsync(Guid caseId, CriarSolicitacaoAssistenteRequest request, CancellationToken ct)
    {
        var include = request.Contexto?.IncluirDadosCaso == true;
        var item = include ? await cases.ObterCasoAsync(caseId, auth.OrganizationId, ct) : null;
        var people = include ? await cases.ListarEnvolvidosAsync(caseId, auth.OrganizationId, ct) : [];
        var contextIds = request.Contexto?.ContextoCasoIds ?? [];
        var contexts = new List<ContextoCaso>();
        foreach (var id in contextIds.Order()) contexts.Add(await cases.ObterContextoAsync(caseId, id, auth.OrganizationId, ct) ?? throw Changed("AI_CONTEXT_SOURCE_CHANGED"));
        var text = new AssistenteContextBuilder(Options.Create(options.CurrentValue)).Criar(request.Instrucao!, request.Contexto!.Documento!, request.Contexto.ContextoAdicional,
            item, people, contexts);
        return text;
    }
    public async Task BindAsync(AssistenteGeracao job, CancellationToken ct)
    {
        var membership = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.OrganizationId == job.OrganizationId && m.UserId == job.UserId, ct);
        if (membership is null || !await db.Users.AnyAsync(u => u.Id == job.UserId && u.Status == UserStatusEnum.Active, ct) ||
            !await db.Organizations.AnyAsync(o => o.Id == job.OrganizationId && o.Status == OrganizationStatusEnum.Active, ct)) throw Changed("AI_GENERATION_ACCESS_REVOKED");
        background.Bind(job.OrganizationId, job.UserId, membership.Role, job.CorrelationId);
    }
    public async Task ValidateAsync(AssistenteGeracao job, CancellationToken ct, bool lockSources = false)
    {
        if (background.Value is not null) await BindAsync(job, ct);
        if (!Enabled || !options.CurrentValue.AutomaticConversationEnabled || !options.CurrentValue.ConversationV2Enabled || !options.CurrentValue.ReviewV2Enabled) throw Changed("AI_GENERATION_DISABLED");
        if (job.Modelo != options.CurrentValue.Model || job.PoliticaVersao != Version) throw Changed("AI_GENERATION_POLICY_CHANGED");
        if (job.PrazoEm <= DateTime.UtcNow) throw Changed("AI_GENERATION_DEADLINE");
        var conversation = await repository.ObterConversaAsync(job.ConversaId, auth.OrganizationId, auth.UserId, false, ct) ?? throw Missing();
        if (conversation.PecaId != job.PecaId || job.OrganizationId != auth.OrganizationId || job.UserId != auth.UserId) throw Missing();
        var user = await repository.ObterMensagemAsync(job.ConversaId, job.PedidoId, ct) ?? throw Missing();
        var request = AssistenteChatService.Read<CriarSolicitacaoAssistenteRequest>(user.Solicitacao)!;
        await features.ExigirModoAsync(auth.OrganizationId, request.Modo, ct); await transparency.ExigirConfirmacaoAsync(ct);
        var piece = await pieces.ObterPecaAsync(job.PecaId, auth.OrganizationId, ct) ?? throw Missing();
        if (piece.Versao != request.VersaoPeca) throw AssistentePatchCompiler.Stale();
        if (await CaseHashAsync(piece.CasoId, job.ConversaId, request, ct) != job.CasoSha256) throw Changed("AI_CONTEXT_SOURCE_CHANGED");
        if (job.LeaseToken is { } lease && !await db.Set<AssistenteGeracao>().AnyAsync(j => j.Id == job.Id && j.LeaseToken == lease && j.LeaseAte > DateTime.UtcNow &&
            j.Estado != "Cancelada" && j.Estado != "Concluida" && j.Estado != "Interrompida" && j.Estado != "Incompleta" && j.Estado != "Desatualizada", ct)) throw Changed("AI_GENERATION_LEASE_LOST");
        if (Read<AssistenteSourceSnapshot[]>(job.FontesIds).Length > 0 && !documents.Enabled) throw Changed("AI_DOCUMENTS_DISABLED");
        if (!await documents.SnapshotValidAsync(Read<AssistenteSourceSnapshot[]>(job.FontesIds), job.ConversaId, piece.CasoId, ct)) throw Changed("AI_DOCUMENT_SOURCE_CHANGED");
        if (user.FontesDocumentais is not null && !await documents.ManifestValidAsync(user.FontesDocumentais, job.ConversaId, piece.CasoId, ct, lockSources)) throw Changed("AI_DOCUMENT_SOURCE_CHANGED");
        if (job.Contexto is not null)
        {
            var saved = Read<AssistenteGenerationContext>(job.Contexto);
            if (saved.Historico is not null && history is not null)
            { var responseGuard = saved.Historico.RespostaId; saved.Historico.RespostaId = null;
                try { await history.ValidateAsync(saved.Historico, ct, lockSources); } finally { saved.Historico.RespostaId = responseGuard; } }
            if (saved.ReferenciaId is { } reference)
            {
                var proposal = await repository.ObterPropostaAsync(reference, auth.OrganizationId, auth.UserId, ct);
                if (proposal?.VersaoDecisoes != saved.ReferenciaVersao) throw Changed("AI_PROPOSAL_REVISION_CONFLICT");
            }
        }
        if (job.Plano is not null)
        {
            await features.ExigirModoAsync(auth.OrganizationId, AssistenteModo.Redigir, ct);
            if (!(await features.ObterAsync(auth.OrganizationId, ct)).Features.Contains(OrganizationFeatureEnum.AI_DOCUMENT_GENERATION)) throw Changed("AI_FEATURE_NOT_ENABLED");
        }
    }
    public async Task<AssistenteGeracaoDto> GetAsync(Guid conversation, Guid id, CancellationToken ct)
    {
        var chat = await repository.ObterConversaAsync(conversation, auth.OrganizationId, auth.UserId, false, ct) ?? throw Missing();
        if (await pieces.ObterPecaAsync(chat.PecaId, auth.OrganizationId, ct) is null) throw Missing();
        var job = await db.Set<AssistenteGeracao>().AsNoTracking().SingleOrDefaultAsync(j => j.Id == id && j.ConversaId == conversation && j.OrganizationId == auth.OrganizationId && j.UserId == auth.UserId, ct) ?? throw Missing();
        IReadOnlyList<AssistenteGenerationSection> sections = job.Plano is null ? [] : Read<AssistenteGenerationPlan>(job.Plano).Secoes;
        var steps = await db.Set<AssistenteGeracaoEtapa>().AsNoTracking().Where(s => s.GeracaoId == id && s.Estado == "Confirmada" && s.Tipo.StartsWith("Secao:")).Select(s => s.Tipo).ToArrayAsync(ct);
        return new(id, job.Estado, steps.Distinct().Count(), sections.Count, sections.Select(s => s.Titulo).ToArray(), job.CodigoErro, job.AnaliseId);
    }
    public async Task<(int Calls, int Tokens)> BudgetAsync(AssistenteGeracao job, CancellationToken ct)
    {
        var steps = await db.Set<AssistenteGeracaoEtapa>().AsNoTracking().Where(s => s.GeracaoId == job.Id).ToArrayAsync(ct);
        var ledger = await db.AiUsageLedger.AsNoTracking().Where(l => l.OrganizationId == job.OrganizationId && l.ClientRequestId == job.ClientRequestId && l.UserId == job.UserId).ToArrayAsync(ct);
        long tokens = steps.Sum(s => (long)(s.TokensLiquidados > 0 ? s.TokensLiquidados : s.TokensReservados));
        var unmatched = ledger.Where(l => !steps.Any(s => s.SolicitacaoId == l.SolicitacaoId && s.Chamada == l.AttemptCount)).ToArray();
        tokens += unmatched.Sum(l => (long)(l.InputTokens ?? options.CurrentValue.MaxInputTokens) + (l.OutputTokens ?? options.CurrentValue.MaxOutputTokens));
        if (job.AnaliseId is { } aid)
        {
            var unknown = await db.Set<AssistenteAnaliseLote>().AsNoTracking().Where(b => b.AnaliseId == aid && b.TokensReservados > 0 && (b.Estado == "Chamando" || b.Estado == "Indeterminado")).ToArrayAsync(ct);
            var missing = unknown.Where(b => ledger.All(l => l.SolicitacaoId != b.Id)).ToArray();
            tokens += missing.Sum(b => (long)b.TokensReservados);
            return (steps.Length + unmatched.Length + missing.Length, (int)Math.Min(int.MaxValue, tokens));
        }
        return (steps.Length + unmatched.Length, (int)Math.Min(int.MaxValue, tokens));
    }
    public async Task ReserveAsync(AssistenteGeracao job, Guid solicitation, int ordinal, string type, int input, int output, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var live = await LockedAsync(job.Id, ct);
        if (live.LeaseToken != job.LeaseToken || live.LeaseAte <= DateTime.UtcNow || !Active(live.Estado)) throw Changed("AI_GENERATION_LEASE_LOST");
        var used = await BudgetAsync(job, ct);
        if (used.Calls >= options.CurrentValue.GenerationMaxCalls || (long)used.Tokens + input + output > options.CurrentValue.GenerationMaxTotalTokens) throw Changed("AI_GENERATION_LIMIT");
        db.Add(new AssistenteGeracaoEtapa { Id = Guid.NewGuid(), OrganizationId = job.OrganizationId, GeracaoId = job.Id, SolicitacaoId = solicitation,
            Chamada = ordinal, Tipo = type, TokensReservados = checked(input + output), CriadaEm = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
    }
    public Task RecordAsync(AssistenteGeracao job, Guid solicitation, AssistenteProviderCall call, CancellationToken ct) =>
        db.Set<AssistenteGeracaoEtapa>().Where(s => s.GeracaoId == job.Id && s.SolicitacaoId == solicitation && s.Chamada == call.Ordinal)
        .ExecuteUpdateAsync(s => s.SetProperty(s => s.Estado, call.Status == "succeeded" ? "Recebida" : "Indeterminada")
            .SetProperty(s => s.TokensLiquidados, call.InputTokens.HasValue && call.OutputTokens.HasValue ? call.InputTokens.Value + call.OutputTokens.Value : 0), ct);
    public async Task CompleteAsync(AssistenteGeracao? job, CancellationToken ct)
    {
        if (job is null) return;
        var updated = await db.Set<AssistenteGeracao>().Where(j => j.Id == job.Id && j.LeaseToken == job.LeaseToken && j.LeaseAte > DateTime.UtcNow && j.Estado != "Cancelada")
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, "Concluida").SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.LeaseAte, (DateTime?)null).SetProperty(j => j.AtualizadaEm, DateTime.UtcNow), ct);
        if (updated != 1) throw Changed("AI_GENERATION_LEASE_LOST");
    }
    public async Task ActAsync(Guid conversation, Guid id, bool retry, CancellationToken ct)
    {
        await GetAsync(conversation, id, ct);
        await repository.EmTransacaoAsync(async token =>
        {
            await repository.ObterConversaAsync(conversation, auth.OrganizationId, auth.UserId, true, token);
            var job = await LockedAsync(id, token);
            if (retry)
            {
                if (job.Estado != "Interrompida") throw Changed("AI_GENERATION_NOT_RESUMABLE");
                job.LeaseToken = null; await ValidateAsync(job, token);
                if (await repository.ObterMensagemAtivaAsync(conversation, token) is not null) throw Changed("AI_CONVERSATION_BUSY");
                var used = await BudgetAsync(job, token);
                if (used.Calls >= options.CurrentValue.GenerationMaxCalls || used.Tokens >= options.CurrentValue.GenerationMaxTotalTokens) throw Changed("AI_GENERATION_LIMIT");
                await db.Set<AssistenteGeracaoEtapa>().Where(s => s.GeracaoId == id && (s.Estado == "Chamando" || s.Estado == "Recebida")).ExecuteUpdateAsync(s => s.SetProperty(s => s.Estado, "Indeterminada"), token);
            }
            else if (!Active(job.Estado)) return false;
            var resumeChild = false;
            if (retry && job.AnaliseId is { } childId)
            {
                var child = await db.Set<AssistenteAnaliseDocumental>().AsNoTracking().SingleAsync(a => a.Id == childId, token);
                if (child.Estado == "Interrompida")
                {
                    if (child.PrazoEm <= DateTime.UtcNow) throw Changed("AI_GENERATION_DEADLINE");
                    await db.Set<AssistenteAnaliseLote>().Where(b => b.AnaliseId == childId && b.Estado == "Chamando").ExecuteUpdateAsync(s => s.SetProperty(b => b.Estado, "Indeterminado"), token);
                    await db.Set<AssistenteAnaliseDocumental>().Where(a => a.Id == childId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Estado, child.Resumo == null ? "Lendo" : "Pronta")
                        .SetProperty(a => a.LeaseToken, (Guid?)null).SetProperty(a => a.LeaseAte, (DateTime?)null).SetProperty(a => a.CodigoErro, (string?)null), token);
                    resumeChild = true;
                }
            }
            var state = retry ? resumeChild ? "AguardandoAnalise" : job.Plano is null ? "NaFila" : "Redigindo" : "Cancelada";
            await db.Set<AssistenteGeracao>().Where(j => j.Id == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, state)
                .SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.LeaseAte, (DateTime?)null).SetProperty(j => j.CodigoErro, retry ? null : "AI_CANCELLED"), token);
            await db.AssistenteMensagens.Where(m => m.Id == job.RespostaId && m.Estado != "Concluida").ExecuteUpdateAsync(s => s.SetProperty(m => m.Estado, retry ? resumeChild ? "AguardandoAnalise" : "AguardandoGeracao" : "Cancelada")
                .SetProperty(m => m.CodigoErro, retry ? null : "AI_CANCELLED"), token);
            if (!retry && job.AnaliseId is { } analysisId)
                await db.Set<AssistenteAnaliseDocumental>().Where(a => a.Id == analysisId && a.Estado != "Concluida").ExecuteUpdateAsync(s => s.SetProperty(a => a.Estado, "Cancelada").SetProperty(a => a.LeaseToken, (Guid?)null), token);
            return true;
        }, ct);
    }
    public async Task AwaitAnalysisAsync(Guid generationId, Guid analysisId, CancellationToken ct)
    {
        await db.Set<AssistenteGeracao>().Where(j => j.Id == generationId).ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, "AguardandoAnalise")
            .SetProperty(j => j.AnaliseId, analysisId).SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.LeaseAte, (DateTime?)null), ct);
        await db.Set<AssistenteGeracaoEtapa>().Where(s => s.GeracaoId == generationId && s.Estado == "Recebida").ExecuteUpdateAsync(s => s.SetProperty(s => s.Estado, "Confirmada")
            .SetProperty(s => s.Tipo, "Ferramenta").SetProperty(s => s.Resultado, AssistentePatchCompiler.Json(new { analiseId = analysisId })), ct);
    }
    public async Task ResumeAnalysisAsync(AssistenteAnaliseDocumental analysis, CancellationToken ct)
    {
        var response = await db.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == analysis.RespostaId, ct);
        if (response.GeracaoId is not { } id) throw Missing();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await repository.ObterConversaAsync(analysis.ConversaId, auth.OrganizationId, auth.UserId, true, ct);
        var job = await LockedAsync(id, ct);
        await ValidateAsync(job, ct, true);
        if (job.Estado != "AguardandoAnalise" || job.AnaliseId != analysis.Id) throw Changed("AI_GENERATION_LEASE_LOST");
        var changed = await db.Set<AssistenteAnaliseDocumental>().Where(a => a.Id == analysis.Id && a.LeaseToken == analysis.LeaseToken && a.LeaseAte > DateTime.UtcNow && a.Estado == "Pronta")
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Estado, "Concluida").SetProperty(a => a.LeaseToken, (Guid?)null).SetProperty(a => a.LeaseAte, (DateTime?)null), ct);
        if (changed != 1) throw Changed("AI_GENERATION_LEASE_LOST");
        var saved = Read<AssistenteGenerationContext>(job.Contexto!);
        var sources = AssistenteDocumentAnalysisService.Read<AssistenteFonteRecuperacao[]>(analysis.Fontes);
        var complete = sources.All(s => s.Estado == "Concluido" && s.TrechosExcluidos == 0);
        saved = saved with { Texto = saved.Texto + AssistenteDocumentAnalysisService.SummaryContext(analysis), AnaliseId = analysis.Id,
            Cobertura = new(complete ? "Completa" : "Parcial", "Todos", sources.Select(s => new AssistenteCoberturaFonteDto(s.Id, s.Nome, s.Origem, s.TrechosElegiveis, 0, s.TrechosExcluidos, s.Estado, s.TrechosElegiveis)).ToArray()) };
        if (saved.Texto.Length + saved.Evidencias.Length > options.CurrentValue.MaxTotalContextCharacters) throw Changed("AI_GENERATION_LIMIT");
        await db.Set<AssistenteGeracao>().Where(j => j.Id == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Contexto, AssistentePatchCompiler.Json(saved))
            .SetProperty(j => j.Estado, job.Plano == null ? "NaFila" : "Redigindo").SetProperty(j => j.LeaseAte, (DateTime?)null), ct);
        await db.AssistenteMensagens.Where(m => m.Id == analysis.RespostaId).ExecuteUpdateAsync(s => s.SetProperty(m => m.Estado, "AguardandoGeracao")
            .SetProperty(m => m.ExpiraEm, job.PrazoEm.AddSeconds(30)), ct);
        await tx.CommitAsync(ct);
    }
    public Task<AssistenteGeracao> LockedAsync(Guid id, CancellationToken ct) => db.Set<AssistenteGeracao>()
        .FromSqlInterpolated($"SELECT * FROM ai_generations WHERE id = {id} FOR UPDATE").AsNoTracking().SingleAsync(ct);
    public static T Read<T>(JsonDocument json) => JsonSerializer.Deserialize<T>(json, AssistentePatchCompiler.JsonOptions)!;
    public static AssistenteIaException Changed(string code) => new(409, code, "O processamento foi interrompido; o pedido e as etapas concluídas foram preservados. Confira as fontes, a versão da Peça e os limites antes de retomar.");
    private static AssistenteIaException Missing() => new(404, "AI_RESOURCE_NOT_FOUND", "Execução não encontrada.");
}
