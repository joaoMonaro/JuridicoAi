using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
namespace JuridicoAI.Api.Application.Services;

// No IAuthContext, Peça/Case repository, feature checks or provider: this surface only reads persisted user-owned content.
public sealed class PersonalHistoryService(JuridicoAiDbContext db, IPersonalHistoryContext identity, IFileStorage storage)
{
    private IQueryable<AssistenteConversa> Conversations => db.AssistenteConversas.AsNoTracking().Where(c => c.UserId == identity.UserId);
    public async Task<PersonalConversationPage> ListAsync(string? before, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 50 || before?.Length > 500) throw BadPage();
        var query = Conversations;
        if (before is not null)
        {
            Cursor cursor;
            try { cursor = JsonSerializer.Deserialize<Cursor>(WebEncoders.Base64UrlDecode(before), AssistentePatchCompiler.JsonOptions) ?? throw BadPage(); }
            catch (Exception e) when (e is JsonException or FormatException or ArgumentException) { throw BadPage(); }
            if (cursor.At.Kind != DateTimeKind.Utc || cursor.Id == Guid.Empty) throw BadPage();
            query = query.Where(c => c.AtualizadaEm < cursor.At || c.AtualizadaEm == cursor.At && c.Id.CompareTo(cursor.Id) < 0);
        }
        var rows = await query.OrderByDescending(c => c.AtualizadaEm).ThenByDescending(c => c.Id).Take(limit + 1).ToArrayAsync(ct);
        var selected = rows.Take(limit).ToArray(); var last = selected.LastOrDefault();
        return new(selected.Select(Dto).ToArray(), rows.Length > limit && last is not null
            ? WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Cursor(last.AtualizadaEm, last.Id), AssistentePatchCompiler.JsonOptions)) : null);
    }
    public async Task<PersonalConversationDto> GetAsync(Guid id, CancellationToken ct) => Dto(await OwnAsync(id, ct));
    public async Task<IReadOnlyList<PersonalMessageDto>> MessagesAsync(Guid id, long? before, int limit, CancellationToken ct)
    {
        await OwnAsync(id, ct);
        if (before is < 1 || limit is < 1 or > 100) throw BadPage();
        var rows = await db.AssistenteMensagens.AsNoTracking().Where(m => m.ConversaId == id && (!before.HasValue || m.Sequencia < before.Value))
            .OrderByDescending(m => m.Sequencia).Take(limit).ToArrayAsync(ct);
        // Do not return execution IDs, source catalogs/manifests, raw request/result JSON or internal snapshots.
        return rows.OrderBy(m => m.Sequencia).Select(m =>
        {
            var result = AssistenteChatService.Read<AssistenteSolicitacaoConcluidaDto>(m.Resultado);
            var request = AssistenteChatService.Read<CriarSolicitacaoAssistenteRequest>(m.Solicitacao);
            return new PersonalMessageDto(m.Id, m.Sequencia, m.Autor, m.Texto, m.CriadaEm,
                m.Estado is "Concluida" or "Rejeitada" ? m.Estado : "SemRespostaConcluida", result?.Sugestao.Titulo,
                m.Autor == "Usuario" ? request?.Contexto?.ContextoAdicional : null, m.PropostaId, result?.Sugestao.Alertas ?? []);
        }).ToArray();
    }
    public async Task<IReadOnlyList<PersonalAttachmentDto>> AttachmentsAsync(Guid id, CancellationToken ct)
    {
        await OwnAsync(id, ct);
        return await db.AssistenteDocumentos.AsNoTracking().Where(d => d.ConversaId == id).OrderBy(d => d.CriadoEm).ThenBy(d => d.Id)
            .Select(d => new PersonalAttachmentDto(d.Id, d.Nome, d.TamanhoBytes, d.CriadoEm)).ToArrayAsync(ct);
    }
    public async Task<(Stream Stream, string Name, string ContentType)> DownloadAsync(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var conversation = await OwnAsync(id, ct);
        var attachment = await db.AssistenteDocumentos.AsNoTracking().SingleOrDefaultAsync(d => d.Id == attachmentId && d.ConversaId == id && d.OrganizationId == conversation.OrganizationId, ct) ?? throw Missing();
        if (!PrivatePath(conversation, attachment.StorageId)) throw Missing();
        try { return (await storage.GetAsync(attachment.StorageId, ct), attachment.Nome, "application/octet-stream"); }
        catch (FileNotFoundException) { throw Missing(); }
    }
    public async Task<PersonalProposalDto> ProposalAsync(Guid id, CancellationToken ct)
    {
        var proposal = await db.AssistentePropostas.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && Conversations.Any(c => c.Id == p.ConversaId && c.OrganizationId == p.OrganizationId) &&
            db.AssistenteMensagens.Any(m => m.ConversaId == p.ConversaId && m.PropostaId == p.Id && m.Autor == "Assistente"), ct) ?? throw Missing();
        var groups = AssistenteChatService.Read<AssistenteGrupoPersistido[]>(proposal.Grupos)!;
        return new(id, proposal.ConversaId, groups.Select(g => new PersonalProposalGroup(g.Id, g.Estado, g.Explicacao, g.Original, g.Proposto)).ToArray());
    }
    public async Task<AssistantDeletionDto> DeleteAsync(Guid id, DeleteConversationRequest request, CancellationToken ct)
    {
        if (!request.ConfirmarExclusao || request.RequestId == Guid.Empty) throw new AssistenteIaException(400, "AI_DELETION_CONFIRMATION_REQUIRED", "Confirme explicitamente a exclusão desta conversa e dos seus anexos privados.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize concurrent request IDs for the same owner, including the case where the content has already been purged.
        var owner = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {identity.UserId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (owner?.Status != UserStatusEnum.Active) throw new NaoAutorizadoException("Conta pessoal indisponível.");
        var previous = await db.Set<AssistenteExclusao>().AsNoTracking().SingleOrDefaultAsync(d => d.UserId == identity.UserId && d.Id == request.RequestId, ct)
            ?? await db.Set<AssistenteExclusao>().AsNoTracking().SingleOrDefaultAsync(d => d.UserId == identity.UserId && d.ConversaId == id, ct);
        if (previous is not null)
        {
            if (previous.Id == request.RequestId && previous.ConversaId != id) throw new AssistenteIaException(409, "AI_IDEMPOTENCY_MISMATCH", "O identificador de exclusão já foi utilizado para outra conversa.");
            await tx.CommitAsync(ct); return DeletionDto(previous);
        }
        var conversation = await db.AssistenteConversas.FromSqlInterpolated($"SELECT * FROM ai_conversations WHERE id = {id} AND user_id = {identity.UserId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct) ?? throw Missing();
        var files = await db.AssistenteDocumentos.AsNoTracking().Where(d => d.ConversaId == id).Select(d => d.StorageId).ToArrayAsync(ct);
        if (files.Any(f => !PrivatePath(conversation, f))) throw new AssistenteIaException(409, "AI_DELETION_STORAGE_SCOPE_INVALID", "A exclusão requer revisão do armazenamento privado.");
        var now = DateTime.UtcNow;
        await db.AssistenteConversas.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "EmExclusao").SetProperty(c => c.ExclusaoEpoch, c => c.ExclusaoEpoch + 1), ct);
        await db.Set<AssistenteGeracao>().Where(j => j.ConversaId == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, "Cancelada").SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.LeaseAte, (DateTime?)null), ct);
        await db.Set<AssistenteAnaliseDocumental>().Where(j => j.ConversaId == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, "Cancelada").SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.LeaseAte, (DateTime?)null), ct);
        await db.AssistenteDocumentos.Where(d => d.ConversaId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, "Cancelado").SetProperty(d => d.LeaseToken, (Guid?)null).SetProperty(d => d.LeaseAte, (DateTime?)null), ct);
        await db.AssistenteMensagens.Where(m => m.ConversaId == id && (m.Estado == "Gerando" || m.Estado == "AguardandoAnalise" || m.Estado == "AguardandoGeracao"))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Estado, "Cancelada").SetProperty(m => m.CodigoErro, "AI_CONVERSATION_DELETING"), ct);
        var deletion = new AssistenteExclusao { Id = request.RequestId, ConversaId = id, UserId = identity.UserId, Arquivos = AssistentePatchCompiler.Json(files), AutorizadaEm = now, DisponivelEm = now };
        db.Add(deletion); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return DeletionDto(deletion);
    }
    public async Task<AssistantDeletionDto> DeletionAsync(Guid requestId, CancellationToken ct) => DeletionDto(await db.Set<AssistenteExclusao>().AsNoTracking().SingleOrDefaultAsync(d => d.Id == requestId && d.UserId == identity.UserId, ct) ?? throw Missing());
    public async Task<IReadOnlyList<AssistantDeletionDto>> DeletionsAsync(CancellationToken ct) => (await db.Set<AssistenteExclusao>().AsNoTracking().Where(d => d.UserId == identity.UserId)
        .OrderByDescending(d => d.AutorizadaEm).ThenByDescending(d => d.Id).Take(30).ToArrayAsync(ct)).Select(DeletionDto).ToArray();
    private async Task<AssistenteConversa> OwnAsync(Guid id, CancellationToken ct) => await Conversations.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw Missing();
    private static PersonalConversationDto Dto(AssistenteConversa c) => new(c.Id, c.Titulo, c.CriadaEm, c.AtualizadaEm);
    public static AssistantDeletionDto DeletionDto(AssistenteExclusao d) => new(d.Id, d.ConversaId, d.Estado, d.AutorizadaEm, d.ConcluidaEm, d.CodigoErro);
    private static bool PrivatePath(AssistenteConversa c, string path) => path.StartsWith($"assistant/{c.OrganizationId:N}/{c.Id:N}/", StringComparison.Ordinal) && !path.Contains("..", StringComparison.Ordinal) && !path.Contains('\\');
    private static AssistenteIaException Missing() => new(404, "AI_HISTORY_NOT_FOUND", "Histórico privado não encontrado.");
    private static AssistenteIaException BadPage() => new(400, "AI_HISTORY_PAGE_INVALID", "Paginação do histórico inválida.");
    private sealed record Cursor(DateTime At, Guid Id);
}
