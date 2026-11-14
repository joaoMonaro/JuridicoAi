using System.Net;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Documents;
using JuridicoAI.Api.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;
public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task Geracao_LimiteLocalReavaliaCapacidadeSemEsperarOutraJanelaNemChamarModeloAntesDaLiberacao()
    {
        await using var db = await Database.Create();
        var storage = new DocumentMemoryStorage(); var model = new GenerationModel { AnswerOnly = true };
        var (_, start) = await QueueGeneration(db, storage, model);
        var gate = new LocalCapacityGate();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var scope = db.Context();
            Assert.True(await GenerationServices(db, scope, storage, model, gate).Processor.ProcessNextAsync(default));
            var job = await scope.Set<AssistenteGeracao>().AsNoTracking().SingleAsync();
            Assert.Equal("NaFila", job.Estado);
            Assert.NotNull(job.LeaseAte);
            // A whole-window Retry-After must not postpone the next local check by 60s.
            Assert.InRange((job.LeaseAte.Value - DateTime.UtcNow).TotalSeconds, -1, 2.1);
            Assert.Equal(0, model.Calls);
            Assert.Empty(await scope.AiUsageLedger.ToArrayAsync());
            Assert.Empty(await scope.Set<AssistenteGeracaoEtapa>().ToArrayAsync());
            Assert.False(await GenerationServices(db, scope, storage, model, gate).Processor.ProcessNextAsync(default));
            var wait = job.LeaseAte.Value - DateTime.UtcNow + TimeSpan.FromMilliseconds(50);
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
        }
        gate.Open = true;
        await using var ready = db.Context();
        Assert.True(await GenerationServices(db, ready, storage, model, gate).Processor.ProcessNextAsync(default));
        Assert.Equal("Concluida", (await ready.Set<AssistenteGeracao>().AsNoTracking().SingleAsync()).Estado);
        Assert.Equal(1, model.Calls);
        Assert.Single(await ready.AiUsageLedger.ToArrayAsync());
        Assert.Empty(await ready.AssistentePropostas.ToArrayAsync());
        Assert.Equal("Resposta preservada.", (await ready.AssistenteMensagens.SingleAsync(m => m.Id == start.Resposta.Id)).Texto);
    }

    private sealed class LocalCapacityGate : IAssistenteUsageLimiter
    {
        public bool Open;
        public ValueTask<IAsyncDisposable> AdquirirAsync(Guid organization, Guid user, CancellationToken ct)
        {
            if (!Open) throw new AssistenteIaException(429, "AI_USER_RATE_LIMITED", "Limite local sintético.",
                retryable: true, retryAfter: TimeSpan.FromMinutes(1));
            return ValueTask.FromResult<IAsyncDisposable>(new Released());
        }
        private sealed class Released : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }

    [ReviewPostgresFact]
    public async Task Geracao_FilaAntesDoModeloERespostaNormalSobreviveAoFechamentoSemDuplicarPedido()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel { AnswerOnly = true };
        var (conversation, start) = await QueueGeneration(db, storage, model);
        Assert.Equal(0, model.Calls); Assert.Equal("AguardandoGeracao", start.Resposta.Estado);
        await using (var scope = db.Context())
        {
            var services = GenerationServices(db, scope, storage, model);
            var request = AssistenteChatService.Read<CriarSolicitacaoAssistenteRequest>(start.Pedido.Solicitacao)!;
            Assert.False((await services.Chat.IniciarAsync(conversation, request, default)).Novo);
            Assert.Equal("AI_CONVERSATION_BUSY", (await Assert.ThrowsAsync<AssistenteIaException>(() => services.Chat.IniciarAsync(conversation, Free("Outro"), default))).Code);
        }
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); var response = await verify.AssistenteMensagens.SingleAsync(m => m.Id == start.Resposta.Id);
        Assert.True(response.Estado == "Concluida", response.CodigoErro); Assert.Equal("Resposta preservada.", AssistenteChatService.Dto(response).Resultado!.Sugestao.Texto);
        Assert.Equal(1, model.Calls); Assert.Equal(2, await verify.AssistenteMensagens.CountAsync()); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
        Assert.False(await GenerationServices(db, verify, storage, model).Processor.ProcessNextAsync(default));
    }

    [ReviewPostgresFact]
    public async Task Geracao_MinutaMaiorQueSaidaUnicaSalvaPartesRevisaEPublicaUmaPropostaAprovavel()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel();
        var (_, start) = await QueueGeneration(db, storage, model);
        for (var i = 0; i < 4; i++)
        {
            await using var scope = db.Context(); Assert.True(await GenerationServices(db, scope, storage, model).Processor.ProcessNextAsync(default));
            Assert.Empty(await scope.AssistentePropostas.ToArrayAsync()); Assert.Equal(1, (await scope.Pecas.AsNoTracking().SingleAsync()).Versao);
        }
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); var response = AssistenteChatService.Dto(await verify.AssistenteMensagens.SingleAsync(m => m.Id == start.Resposta.Id));
        var proposal = response.Resultado!.Proposta!; Assert.NotNull(proposal); Assert.Single(proposal.Grupos);
        Assert.Equal(3, await verify.Set<AssistenteGeracaoEtapa>().CountAsync(s => s.Estado == "Confirmada" && s.Tipo.StartsWith("Secao:")));
        Assert.Contains(await verify.Set<AssistenteGeracaoEtapa>().ToArrayAsync(), s => s.Tipo == "Revisao");
        Assert.Equal(model.Calls, await verify.AiUsageLedger.CountAsync()); Assert.Equal(5, model.Calls);
        Assert.Equal(1, (await verify.Pecas.AsNoTracking().SingleAsync()).Versao);
        await db.Service(verify).AplicarAsync(proposal.Id, Approve(proposal, proposal.Grupos[0].Id), default);
        var piece = await verify.Pecas.AsNoTracking().SingleAsync(); Assert.Equal(2, piece.Versao);
        Assert.True(piece.ConteudoJson.RootElement.GetRawText().Length > db.Settings.MaxOutputCharacters);
        foreach (var id in new[] { "secao-1", "secao-2", "secao-3" }) Assert.Contains(id, piece.ConteudoJson.RootElement.GetRawText());
        Assert.Equal(2, await verify.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Geracao_RevisaoReprovadaEntregaPendenciasERascunhoNaCorrecaoAntesDePublicar()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel { RejectReview = true };
        await QueueGeneration(db, storage, model); await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); Assert.True((await verify.Set<AssistenteGeracao>().SingleAsync()).Estado == "Concluida", (await verify.Set<AssistenteGeracao>().SingleAsync()).CodigoErro);
        Assert.True(model.SawCorrection); Assert.Equal(3, await verify.Set<AssistenteGeracaoEtapa>().CountAsync(s => s.Estado == "Substituida"));
        Assert.Single(await verify.AssistentePropostas.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Geracao_QuedaComChamadaIncertaNaoRepeteConsumoERetomadaMantemPartesConfirmadas()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel();
        var (conversation, start) = await QueueGeneration(db, storage, model); Guid confirmed;
        await using (var first = db.Context()) Assert.True(await GenerationServices(db, first, storage, model).Processor.ProcessNextAsync(default));
        await using (var first = db.Context())
        {
            Assert.True(await GenerationServices(db, first, storage, model).Processor.ProcessNextAsync(default));
            confirmed = (await first.Set<AssistenteGeracaoEtapa>().SingleAsync(s => s.Tipo == "Secao:secao-1")).Id;
            first.Add(new AssistenteGeracaoEtapa { Id = Guid.NewGuid(), OrganizationId = db.OrgId, GeracaoId = start.Resposta.GeracaoId!.Value,
                SolicitacaoId = Guid.NewGuid(), Chamada = 1, Tipo = "Secao:secao-2", TokensReservados = 900, CriadaEm = DateTime.UtcNow });
            first.Add(new AssistenteGeracaoEtapa { Id = Guid.NewGuid(), OrganizationId = db.OrgId, GeracaoId = start.Resposta.GeracaoId!.Value,
                SolicitacaoId = Guid.NewGuid(), Chamada = 1, Estado = "Recebida", Tipo = "Secao:secao-2", TokensReservados = 900, TokensLiquidados = 380, CriadaEm = DateTime.UtcNow });
            await first.SaveChangesAsync();
        }
        var calls = model.Calls;
        await using (var restart = db.Context())
        {
            var parts = GenerationServices(db, restart, storage, model); Assert.True(await parts.Processor.ProcessNextAsync(default));
            Assert.Equal(calls, model.Calls); Assert.Equal("Interrompida", (await parts.Generations.GetAsync(conversation, start.Resposta.GeracaoId!.Value, default)).Estado);
            await parts.Generations.ActAsync(conversation, start.Resposta.GeracaoId.Value, true, default);
        }
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); Assert.Equal("Confirmada", (await verify.Set<AssistenteGeracaoEtapa>().SingleAsync(s => s.Id == confirmed)).Estado);
        Assert.True((await verify.Set<AssistenteGeracao>().SingleAsync()).Estado == "Concluida", (await verify.Set<AssistenteGeracao>().SingleAsync()).CodigoErro);
        Assert.Contains(await verify.Set<AssistenteGeracaoEtapa>().ToArrayAsync(), s => s.Estado == "Indeterminada");
    }

    [ReviewPostgresFact]
    public async Task Geracao_LimiteGlobalBloqueiaPropostaParcialPreservandoPedidoEPartes()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel();
        await QueueGeneration(db, storage, model);
        await using (var first = db.Context()) Assert.True(await GenerationServices(db, first, storage, model).Processor.ProcessNextAsync(default));
        db.Settings.GenerationMaxCalls = 3; await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); Assert.True((await verify.Set<AssistenteGeracao>().SingleAsync()).Estado == "Incompleta", (await verify.Set<AssistenteGeracao>().SingleAsync()).CodigoErro);
        Assert.True(model.Calls <= 3); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
        Assert.NotEmpty(await verify.Set<AssistenteGeracaoEtapa>().Where(s => s.Tipo.StartsWith("Secao:") && s.Estado == "Confirmada").ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Geracao_CancelarDuranteModeloBloqueiaPublicacaoTardiaEExecucaoConcorrente()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new GenerationModel { BeforeGeneration = async () => { entered.TrySetResult(); await release.Task; } };
        var (conversation, start) = await QueueGeneration(db, storage, model);
        await using var one = db.Context(); var running = GenerationServices(db, one, storage, model).Processor.ProcessNextAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var other = db.Context())
        {
            var parts = GenerationServices(db, other, storage, model); Assert.False(await parts.Processor.ProcessNextAsync(default));
            await parts.Generations.ActAsync(conversation, start.Resposta.GeracaoId!.Value, false, default);
        }
        release.TrySetResult(); Assert.True(await running);
        await using var verify = db.Context(); Assert.Equal("Cancelada", (await verify.Set<AssistenteGeracao>().SingleAsync()).Estado);
        Assert.Empty(await verify.AssistentePropostas.ToArrayAsync()); Assert.Equal(1, model.Calls);
    }

    [ReviewPostgresFact]
    public async Task Geracao_PecaAlteradaEPermissaoRevogadaInvalidamAntesDeNovaChamada()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel();
        var (conversation, start) = await QueueGeneration(db, storage, model);
        await using (var first = db.Context()) Assert.True(await GenerationServices(db, first, storage, model).Processor.ProcessNextAsync(default));
        await using (var edit = db.Context()) await edit.Pecas.ExecuteUpdateAsync(s => s.SetProperty(p => p.Versao, 2));
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); Assert.Equal("Desatualizada", (await verify.Set<AssistenteGeracao>().SingleAsync()).Estado);
        Assert.Equal(1, model.Calls); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
        Assert.Equal(404, (await Assert.ThrowsAsync<AssistenteIaException>(() => GenerationServices(db, verify, storage, model).Generations.GetAsync(Guid.NewGuid(), start.Resposta.GeracaoId!.Value, default))).StatusCode);
        var next = await GenerationServices(db, verify, storage, model).Chat.IniciarAsync(conversation, Free("Gere novamente") with { VersaoPeca = 2 }, default);
        await verify.Memberships.ExecuteDeleteAsync(); await DrainGeneration(db, storage, model);
        Assert.Equal(1, model.Calls); Assert.Equal("AI_GENERATION_ACCESS_REVOKED", (await verify.Set<AssistenteGeracao>().AsNoTracking().SingleAsync(j => j.Id == next.Resposta.GeracaoId)).CodigoErro);
    }

    [ReviewPostgresFact]
    public async Task Geracao_AnaliseDocumentalDelegaERetomaMesmoPedidoAntesDaMinuta()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel { AnalyzeFirst = true };
        var (_, start) = await QueueGeneration(db, storage, model, documents: true);
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); var parent = await verify.Set<AssistenteGeracao>().SingleAsync();
        Assert.Equal("Concluida", parent.Estado); Assert.NotNull(parent.AnaliseId);
        Assert.Equal("Concluida", (await verify.Set<AssistenteAnaliseDocumental>().SingleAsync()).Estado);
        var result = AssistenteChatService.Dto(await verify.AssistenteMensagens.SingleAsync(m => m.Id == start.Resposta.Id)).Resultado!;
        Assert.NotNull(result.Proposta); Assert.Equal("Todos", result.CoberturaDocumental!.EscopoSolicitado); Assert.Equal("Completa", result.CoberturaDocumental.Estado);
        Assert.Equal(model.Calls + model.Analysis.GenerationCalls, await verify.AiUsageLedger.CountAsync()); Assert.Equal(2, await verify.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Geracao_DocumentoAindaProcessandoNaoOcupaModeloEConjuntoFicaFixado()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel { AnalyzeFirst = true };
        var (conversation, _) = await QueueGeneration(db, storage, model, documents: true, extract: false);
        await using (var pending = db.Context())
        {
            Assert.True(await GenerationServices(db, pending, storage, model).Processor.ProcessNextAsync(default)); Assert.Equal(0, model.Calls);
            Assert.Equal("AguardandoDocumentos", (await pending.Set<AssistenteGeracao>().SingleAsync()).Estado);
            await Upload(GenerationServices(db, pending, storage, model).Documents, conversation, "posterior.txt", "Documento posterior, não deve entrar no pedido anterior.");
        }
        await using (var extraction = db.Context()) { while (await Processor(extraction, storage).ProcessNextAsync(default)) { } }
        await using (var ready = db.Context()) await ready.Set<AssistenteGeracao>().ExecuteUpdateAsync(s => s.SetProperty(j => j.LeaseAte, (DateTime?)null));
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); var parent = await verify.Set<AssistenteGeracao>().SingleAsync(); Assert.Equal("Concluida", parent.Estado);
        Assert.Single(AssistenteDocumentAnalysisService.Read<AssistenteFonteRecuperacao[]>((await verify.Set<AssistenteAnaliseDocumental>().SingleAsync()).Fontes));
    }

    [ReviewPostgresFact]
    public async Task Geracao_SecaoInvalidaNaoConfirmaRascunhoNemCriaProposta()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel { InvalidSection = true };
        await QueueGeneration(db, storage, model); await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); Assert.Equal("AI_GENERATION_SECTION_INVALID", (await verify.Set<AssistenteGeracao>().SingleAsync()).CodigoErro);
        Assert.Empty(await verify.AssistentePropostas.ToArrayAsync()); Assert.Empty(await verify.Set<AssistenteGeracaoEtapa>().Where(s => s.Estado == "Confirmada" && s.Tipo.StartsWith("Secao:")).ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Geracao_PublicacaoFalhaRetomaResultadoConfirmadoSemOutraChamadaPaga()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel { AnswerOnly = true };
        var (conversation, start) = await QueueGeneration(db, storage, model);
        await using (var failure = db.Context(new FailAnswer())) Assert.True(await GenerationServices(db, failure, storage, model).Processor.ProcessNextAsync(default));
        Assert.Equal(1, model.Calls);
        await using (var retry = db.Context())
        {
            Assert.Equal("Interrompida", (await retry.Set<AssistenteGeracao>().SingleAsync()).Estado);
            Assert.Equal("Confirmada", (await retry.Set<AssistenteGeracaoEtapa>().SingleAsync()).Estado);
            await GenerationServices(db, retry, storage, model).Generations.ActAsync(conversation, start.Resposta.GeracaoId!.Value, true, default);
        }
        await DrainGeneration(db, storage, model); Assert.Equal(1, model.Calls);
        await using var verify = db.Context(); Assert.Equal("Concluida", (await verify.Set<AssistenteGeracao>().SingleAsync()).Estado);
        Assert.Equal(start.Pedido.Id, AssistenteChatService.Dto(await verify.AssistenteMensagens.SingleAsync(m => m.Id == start.Resposta.Id)).Resultado!.Sugestao.MensagemUsuarioId);
    }

    [ReviewPostgresFact]
    public async Task Geracao_MemoriaCompactadaPersisteEntrePlanoSecoesERevisaoSemRecompactar()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel();
        var (conversation, start) = await QueueGeneration(db, storage, model, history: true);
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); Assert.Equal("Concluida", (await verify.Set<AssistenteGeracao>().SingleAsync()).Estado);
        Assert.Equal(1, model.MemoryUpdates); Assert.Single(await verify.Set<AssistenteMemoria>().ToArrayAsync());
        var response = AssistenteChatService.Dto(await verify.AssistenteMensagens.SingleAsync(m => m.Id == start.Resposta.Id));
        Assert.Equal(1, response.Resultado!.ContextoHistorico!.VersaoMemoria); Assert.Equal(82, await verify.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Geracao_FonteAlteradaDepoisDoPlanoBloqueiaSeçõesEProposta()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel();
        await QueueGeneration(db, storage, model, documents: true);
        await using (var first = db.Context()) Assert.True(await GenerationServices(db, first, storage, model).Processor.ProcessNextAsync(default));
        await using (var edit = db.Context()) await edit.AssistenteDocumentos.ExecuteUpdateAsync(s => s.SetProperty(d => d.Sha256, new string('a', 64)));
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context(); Assert.Equal("Desatualizada", (await verify.Set<AssistenteGeracao>().SingleAsync()).Estado);
        Assert.Equal("AI_DOCUMENT_SOURCE_CHANGED", (await verify.Set<AssistenteGeracao>().SingleAsync()).CodigoErro);
        Assert.Equal(1, model.Calls); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
    }

    private static async Task<(Guid, AssistenteChatInicio)> QueueGeneration(Database db, DocumentMemoryStorage storage, GenerationModel model, bool documents = false, bool extract = true, bool history = false)
    {
        db.Settings.Enabled = true; db.Settings.DurableGenerationEnabled = true; db.Settings.HistoryMemoryEnabled = true; db.Settings.Model = "fixture-generation"; db.Settings.ApiKey = "synthetic";
        await using var context = db.Context(); context.Add(new Membership { Id = Guid.NewGuid(), UserId = db.UserId, OrganizationId = db.OrgId, Role = MembershipRoleEnum.Owner, DataCriacao = DateTime.UtcNow, DataAtualizacao = DateTime.UtcNow }); await context.SaveChangesAsync();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default); var parts = GenerationServices(db, context, storage, model);
        if (history) await SeedHistory(db, context, conversation.Id, 40);
        IReadOnlyList<Guid>? ids = null;
        if (documents)
        {
            var source = await Upload(parts.Documents, conversation.Id, "contrato.txt", "Beatriz pagou R$ 876,54 conforme o contrato."); ids = [source.Id];
            if (extract) { await using var extraction = db.Context(); await Processor(extraction, storage).ProcessNextAsync(default); }
        }
        var request = new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Elabore a minuta completa com os fatos apresentados e todos os documentos.", 1, AnexoIds: ids).Solicitar();
        return (conversation.Id, await parts.Chat.IniciarAsync(conversation.Id, request, default));
    }
    private static async Task DrainGeneration(Database db, DocumentMemoryStorage storage, GenerationModel model)
    {
        for (var i = 0; i < 50; i++)
        {
            await using var context = db.Context(); var parent = await context.Set<AssistenteGeracao>().AsNoTracking().OrderByDescending(j => j.CriadaEm).FirstAsync();
            if (!AssistenteGenerationService.Active(parent.Estado)) return;
            var parts = GenerationServices(db, context, storage, model);
            if (parent.Estado == "AguardandoAnalise") Assert.True(await parts.AnalysisProcessor.ProcessNextAsync(default));
            else Assert.True(await parts.Processor.ProcessNextAsync(default));
        }
        Assert.Fail("Geração não atingiu estado terminal.");
    }
    private static (AssistenteChatService Chat, AssistenteGenerationService Generations, AssistenteGenerationProcessor Processor, AssistenteDocumentAnalysisProcessor AnalysisProcessor, AssistenteDocumentService Documents)
        GenerationServices(Database db, JuridicoAiDbContext context, DocumentMemoryStorage storage, GenerationModel model,
            IAssistenteUsageLimiter? capacity = null)
    {
        var repository = new AssistenteWorkspaceRepository(context); var pieces = new PecaRepository(context); var cases = new CasoRepository(context);
        var identity = new AssistenteBackgroundIdentity(); var auth = new TestAuthContext(db.OrgId, db.UserId); var monitor = new Monitor(db.Settings); var correlation = new AnalysisCorrelation(); var config = Options.Create(AnalysisSettings());
        var documents = new AssistenteDocumentService(context, repository, pieces, storage, auth, db.Access, new Transparency(), config);
        var history = new AssistenteHistoryService(context, repository, pieces, auth, db.Access, new Transparency(), monitor, documents);
        var generations = new AssistenteGenerationService(context, repository, pieces, cases, auth, db.Access, new Transparency(), documents, monitor, identity, correlation, history);
        var analyses = new AssistenteDocumentAnalysisService(context, repository, pieces, auth, db.Access, new Transparency(), documents, config, monitor, identity, correlation, generations);
        var chat = new AssistenteChatService(repository, pieces, auth, db.Access, new Transparency(), monitor, documents, history, analysis: analyses, generations: generations);
        var provider = new OpenAiAssistenteIaProvider(new HttpClient(new MemoryHandler(model.Send)) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(db.Settings));
        var limiter = capacity ?? new AnalysisLimiter(model.Analysis); var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(context), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
        var generator = new AssistenteIaApplicationService(pieces, cases, auth, provider, provider, new AssistenteContextBuilder(Options.Create(db.Settings)), new AssistentePromptPolicy(), new AssistenteRequestRegistry(), db.Access, new Transparency(), limiter, usage,
            correlation, monitor, NullLogger<AssistenteIaApplicationService>.Instance, config);
        return (chat, generations, new(context, generations, chat, generator, provider, provider, db.Access, usage, limiter, monitor),
            new(context, analyses, documents, chat, generator, provider, provider, limiter, usage, config, monitor, generations), documents);
    }
    private sealed class GenerationModel
    {
        public int Calls; public int MemoryUpdates; public bool AnswerOnly; public bool AnalyzeFirst; public bool RejectReview; public bool SawCorrection; public bool InvalidSection; public Func<Task>? BeforeGeneration;
        public AnalysisModel Analysis { get; } = new();
        public async Task<HttpResponseMessage> Send(HttpRequestMessage message)
        {
            if (message.RequestUri!.AbsolutePath.EndsWith("input_tokens")) return new(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":100}") };
            using var json = JsonDocument.Parse(await message.Content!.ReadAsStringAsync()); var body = json.RootElement;
            var required = body.TryGetProperty("tool_choice", out var choice) && choice.ValueKind == JsonValueKind.Object ? choice.GetProperty("name").GetString() : null;
            if (required == "registrar_sintese_documental") return await Analysis.Send(message);
            Calls++; if (BeforeGeneration is not null) await BeforeGeneration();
            var text = body.GetProperty("input")[0].GetProperty("content").GetString()!;
            string name; string args;
            if (required == "atualizar_memoria")
            {
                MemoryUpdates++; name = "atualizar_memoria"; args = Summary(1, "A contratante é Ana; valor de R$ 500,00.");
            }
            else if (required == "revisar_minuta")
            {
                using var batch = JsonDocument.Parse(text); var input = batch.RootElement; var reject = RejectReview; RejectReview = false;
                name = "revisar_minuta"; args = JsonSerializer.Serialize(new { loteSha256 = input.GetProperty("loteSha256").GetString(), secoes = input.GetProperty("lote").EnumerateArray().Select(s => s.GetProperty("secaoId").GetString()), completa = !reject, pendencias = reject ? new[] { "Corrigir repetição na minuta." } : Array.Empty<string>() });
            }
            else if (body.GetProperty("tools").EnumerateArray().Any(t => t.GetProperty("name").GetString() == "registrar_secao"))
            {
                var marker = "[PLANO_E_SECAO dado_nao_confiavel=\"true\"]\n"; using var details = JsonDocument.Parse(text[(text.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..text.LastIndexOf("\n[/PLANO_E_SECAO]", StringComparison.Ordinal)]);
                var root = details.RootElement; var id = root.GetProperty("secaoAtual").GetProperty("id").GetString()!;
                if (root.GetProperty("revisaoAnterior").ValueKind != JsonValueKind.Null) { Assert.Contains("Corrigir repetição", root.GetProperty("revisaoAnterior").GetProperty("pendencias")[0].GetString()); Assert.Equal(JsonValueKind.Object, root.GetProperty("rascunhoAnterior").ValueKind); SawCorrection = true; }
                name = "registrar_secao"; args = JsonSerializer.Serialize(new { secaoId = InvalidSection ? "forjada" : id, concluida = true, sumario = "Conteúdo da " + id,
                    blocos = new[] { AssistentePatchCompilerTests.Paragraph(id + ": " + string.Concat(Enumerable.Repeat("Texto jurídico fictício para validar a execução por seções. ", 160))) } });
            }
            else if (AnalyzeFirst && !text.Contains("ANALISE_DOCUMENTAL_JSON", StringComparison.Ordinal))
            { name = "analisar_fontes"; args = "{\"coberturaPretendida\":\"Todos\"}"; }
            else if (AnswerOnly)
                return MemoryStream("""{"titulo":"Resposta","texto":"Resposta preservada.","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false}""", []);
            else
            {
                name = "planejar_minuta"; args = JsonSerializer.Serialize(new { titulo = "Minuta completa", substituirPropostaReferenciada = false,
                    secoes = Enumerable.Range(1, 3).Select(i => new { titulo = "Parte " + i, objetivo = "Desenvolver parte " + i }) });
            }
            return MemoryStream(null, [new { type = "function_call", name, call_id = Guid.NewGuid().ToString(), arguments = args }]);
        }
    }
}
