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
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task Analise_DuravelRetomaLotesConsolidaEPreparaPropostaSemAplicarOuDuplicarPedido()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage();
        var config = AnalysisSettings(); var model = new AnalysisModel();
        var (conversation, start, id) = await QueueAnalysis(db, storage, config, model, large: true);
        await using (var scope = db.Context())
        {
            var parts = AnalysisServices(db, scope, storage, config, model);
            Assert.Equal("AguardandoAnalise", (await scope.AssistenteMensagens.SingleAsync(m => m.Id == start.Resposta.Id)).Estado);
            Assert.False((await parts.Chat.IniciarAsync(conversation, AssistenteChatService.Read<CriarSolicitacaoAssistenteRequest>(start.Pedido.Solicitacao)!, default)).Novo);
            Assert.Equal("AI_CONVERSATION_BUSY", (await Assert.ThrowsAsync<AssistenteIaException>(() => parts.Chat.IniciarAsync(conversation, Free("Novo pedido"), default))).Code);
        }
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); var job = await verify.Set<AssistenteAnaliseDocumental>().SingleAsync(j => j.Id == id);
        Assert.Equal("Concluida", job.Estado); Assert.NotNull(job.Resumo); Assert.Equal(0, job.TokensFinaisReservados);
        Assert.Equal(2, await verify.AssistenteMensagens.CountAsync()); Assert.Equal(1, (await verify.Pecas.SingleAsync()).Versao);
        var batches = await verify.Set<AssistenteAnaliseLote>().AsNoTracking().Where(b => b.AnaliseId == id).ToArrayAsync();
        Assert.True(batches.Count(b => b.Tipo == "Leitura") > 2); Assert.Contains(batches, b => b.Tipo == "Consolidacao");
        Assert.All(batches, b => Assert.Equal("Concluido", b.Estado));
        Assert.Equal(model.GenerationCalls, await verify.AiUsageLedger.CountAsync());
        var response = await verify.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == start.Resposta.Id);
        var result = AssistenteChatService.Dto(response).Resultado!; Assert.NotNull(result.Proposta);
        Assert.Equal("Completa", result.CoberturaDocumental!.Estado); Assert.Equal("Todos", result.CoberturaDocumental.EscopoSolicitado);
        Assert.All(result.CoberturaDocumental.Fontes, f => Assert.Equal(f.TrechosElegiveis, f.TrechosAnalisados));
        var calls = model.GenerationCalls;
        Assert.False(await AnalysisServices(db, verify, storage, config, model).Processor.ProcessNextAsync(default));
        Assert.Equal(calls, model.GenerationCalls);
        await db.Service(verify).AplicarAsync(result.Proposta.Id, Approve(result.Proposta, result.Proposta.Grupos.Single().Id), default);
        Assert.Equal(2, (await verify.Pecas.AsNoTracking().SingleAsync()).Versao);
        var next = await AnalysisServices(db, verify, storage, config, model).Chat.IniciarAsync(conversation, Free("Consulte um detalhe.") with { VersaoPeca = 2 }, default);
        Assert.Equal(id, next.Analise!.Id); Assert.Contains("ANALISE_DOCUMENTAL_JSON", next.Contexto);
        Assert.Equal(calls, model.GenerationCalls);
    }

    [ReviewPostgresFact]
    public async Task Analise_LimiteInterrompeSemPropostaEPreservaLotesConfirmados()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); config.MaxAnalysisCalls = 3;
        var model = new AnalysisModel(); var (_, _, id) = await QueueAnalysis(db, storage, config, model, large: true);
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); var job = await verify.Set<AssistenteAnaliseDocumental>().SingleAsync(j => j.Id == id);
        Assert.Equal("Incompleta", job.Estado); Assert.Equal("AI_ANALYSIS_LIMIT", job.CodigoErro);
        Assert.True(model.GenerationCalls <= 2); Assert.NotEmpty(await verify.Set<AssistenteAnaliseLote>().Where(b => b.Estado == "Concluido").ToArrayAsync());
        Assert.Empty(await verify.AssistentePropostas.ToArrayAsync()); Assert.Equal(2, await verify.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_ReprocessamentoInvalidaCheckpointAntesDeOutroEnvioAoProvedor()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel();
        var (conversation, _, id) = await QueueAnalysis(db, storage, config, model, large: true);
        await using (var first = db.Context()) Assert.True(await AnalysisServices(db, first, storage, config, model).Processor.ProcessNextAsync(default));
        var calls = model.GenerationCalls;
        await using (var change = db.Context())
        {
            var source = await change.AssistenteDocumentos.SingleAsync();
            await AnalysisServices(db, change, storage, config, model).Documents.RetryAsync(conversation, source.Id, default);
        }
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); Assert.Equal("Desatualizada", (await verify.Set<AssistenteAnaliseDocumental>().SingleAsync()).Estado);
        Assert.Equal(calls, model.GenerationCalls); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_RemocaoDeMembershipBloqueiaNovoEnvioSemApagarDocumentosOuLotes()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel();
        var (_, _, id) = await QueueAnalysis(db, storage, config, model, large: true);
        await using (var first = db.Context()) Assert.True(await AnalysisServices(db, first, storage, config, model).Processor.ProcessNextAsync(default));
        var calls = model.GenerationCalls;
        await using (var revoked = db.Context()) await revoked.Memberships.ExecuteDeleteAsync();
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); Assert.Equal("AI_ANALYSIS_ACCESS_REVOKED", (await verify.Set<AssistenteAnaliseDocumental>().SingleAsync()).CodigoErro);
        Assert.Equal(calls, model.GenerationCalls); Assert.Single(await verify.AssistenteDocumentos.ToArrayAsync());
        Assert.NotEmpty(await verify.Set<AssistenteAnaliseLote>().ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_CancelamentoEIsolamentoPreservamCheckpointSemNovaChamada()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel();
        var (conversation, start, id) = await QueueAnalysis(db, storage, config, model, large: true);
        await using var verify = db.Context(); var services = AnalysisServices(db, verify, storage, config, model);
        await services.Analyses.CancelAsync(conversation, id, default);
        Assert.False(await services.Processor.ProcessNextAsync(default)); Assert.Equal(0, model.GenerationCalls);
        Assert.Equal("Cancelada", (await verify.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == start.Resposta.Id)).Estado);
        Assert.Equal(404, (await Assert.ThrowsAsync<AssistenteIaException>(() => services.Analyses.GetAsync(Guid.NewGuid(), id, default))).StatusCode);
        Assert.Equal(2, await verify.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_QuedaDuranteChamadaNaoRepeteConsumoERetomadaExplicitaMantemLotesConfirmados()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel();
        var (conversation, _, id) = await QueueAnalysis(db, storage, config, model, large: true);
        Guid confirmed;
        await using (var first = db.Context())
        {
            Assert.True(await AnalysisServices(db, first, storage, config, model).Processor.ProcessNextAsync(default));
            var leaf = await first.Set<AssistenteAnaliseLote>().AsNoTracking().SingleAsync(); confirmed = leaf.Id;
            first.Add(new AssistenteAnaliseLote { Id = Guid.NewGuid(), AnaliseId = id, OrganizationId = db.OrgId, Ordem = 2, Estado = "Chamando",
                Entradas = JsonDocument.Parse("{}"), Referencias = JsonDocument.Parse("[]"), TokensReservados = 900, CriadaEm = DateTime.UtcNow });
            await first.SaveChangesAsync();
        }
        var calls = model.GenerationCalls;
        await using (var restart = db.Context())
        {
            var services = AnalysisServices(db, restart, storage, config, model);
            Assert.True(await services.Processor.ProcessNextAsync(default)); Assert.Equal(calls, model.GenerationCalls);
            var state = await services.Analyses.GetAsync(conversation, id, default);
            Assert.Equal("Interrompida", state.Estado); Assert.Equal("AI_ANALYSIS_CALL_UNCERTAIN", state.CodigoErro);
            Assert.Equal(1, state.LotesConcluidos);
            await services.Analyses.RetryAsync(conversation, id, default);
        }
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); Assert.Equal("Concluido", (await verify.Set<AssistenteAnaliseLote>().SingleAsync(b => b.Id == confirmed)).Estado);
        Assert.Contains(await verify.Set<AssistenteAnaliseLote>().ToArrayAsync(), b => b.Estado == "Indeterminado");
        Assert.Equal(2, await verify.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_CitacaoInvalidaNaoConfirmaLoteNemProduzProposta()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel { InvalidCitation = true };
        var (_, _, id) = await QueueAnalysis(db, storage, config, model);
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); Assert.Equal("AI_ANALYSIS_SUMMARY_INVALID", (await verify.Set<AssistenteAnaliseDocumental>().SingleAsync()).CodigoErro);
        Assert.Empty(await verify.Set<AssistenteAnaliseLote>().Where(b => b.Estado == "Concluido").ToArrayAsync()); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
        Assert.Equal(1, await verify.AiUsageLedger.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_LacunaDeExtracaoNaoViraCoberturaCompletaNemPermiteRebaixarTodos()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel { FinalScope = "Relevantes" };
        var (_, start, id) = await QueueAnalysis(db, storage, config, model, partial: true);
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); var result = AssistenteChatService.Dto(await verify.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == start.Resposta.Id)).Resultado!;
        Assert.Null(result.Proposta); Assert.Equal("Todos", result.CoberturaDocumental!.EscopoSolicitado); Assert.Equal("Parcial", result.CoberturaDocumental.Estado);
        Assert.True(result.CoberturaDocumental.Fontes.Single().TrechosExcluidos > 0); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_MudancaDaPecaAntesDaRespostaFinalNaoGeraNemAplicaProposta()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel();
        var (_, _, id) = await QueueAnalysis(db, storage, config, model);
        for (var i = 0; i < 12; i++)
        {
            await using var scope = db.Context();
            if ((await scope.Set<AssistenteAnaliseDocumental>().SingleAsync()).Estado == "Pronta") break;
            Assert.True(await AnalysisServices(db, scope, storage, config, model).Processor.ProcessNextAsync(default));
        }
        var calls = model.GenerationCalls;
        await using (var edit = db.Context()) await edit.Pecas.ExecuteUpdateAsync(s => s.SetProperty(p => p.Versao, 2));
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); Assert.Equal("Desatualizada", (await verify.Set<AssistenteAnaliseDocumental>().SingleAsync()).Estado);
        Assert.Equal(calls, model.GenerationCalls); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_WorkersConcorrentesNaoExecutamMesmoLote()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new AnalysisModel { BeforeGeneration = async () => { entered.TrySetResult(); await release.Task; } };
        await QueueAnalysis(db, storage, config, model);
        await using var one = db.Context(); await using var two = db.Context();
        var running = AnalysisServices(db, one, storage, config, model).Processor.ProcessNextAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(await AnalysisServices(db, two, storage, config, model).Processor.ProcessNextAsync(default));
        release.TrySetResult(); Assert.True(await running); Assert.Equal(1, model.GenerationCalls);
        Assert.Single(await two.Set<AssistenteAnaliseLote>().Where(b => b.Estado == "Concluido").ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_CancelarDuranteChamadaBloqueiaPublicacaoTardia()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new AnalysisModel { BeforeGeneration = async () => { entered.TrySetResult(); await release.Task; } };
        var (conversation, _, id) = await QueueAnalysis(db, storage, config, model);
        await using var one = db.Context(); var running = AnalysisServices(db, one, storage, config, model).Processor.ProcessNextAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var cancel = db.Context()) await AnalysisServices(db, cancel, storage, config, model).Analyses.CancelAsync(conversation, id, default);
        release.TrySetResult(); Assert.True(await running);
        await using var verify = db.Context(); Assert.Equal("Cancelada", (await verify.Set<AssistenteAnaliseDocumental>().SingleAsync()).Estado);
        Assert.Empty(await verify.Set<AssistenteAnaliseLote>().Where(b => b.Estado == "Concluido").ToArrayAsync());
        Assert.Empty(await verify.AssistentePropostas.ToArrayAsync()); Assert.Single(await verify.AiUsageLedger.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_PrazoExpiradoNaoExecutaChamadaPaga()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel();
        var (_, _, id) = await QueueAnalysis(db, storage, config, model);
        await using (var change = db.Context()) await change.Set<AssistenteAnaliseDocumental>().ExecuteUpdateAsync(s => s.SetProperty(j => j.PrazoEm, DateTime.UtcNow.AddSeconds(-1)));
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); Assert.Equal("Incompleta", (await verify.Set<AssistenteAnaliseDocumental>().SingleAsync()).Estado);
        Assert.Equal(0, model.GenerationCalls); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_LimiteLocalDeConcorrenciaReagendaSemRepetirChamadaPaga()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel { ThrottleOnce = true };
        var (_, _, id) = await QueueAnalysis(db, storage, config, model);
        await using (var first = db.Context())
        {
            Assert.True(await AnalysisServices(db, first, storage, config, model).Processor.ProcessNextAsync(default));
            Assert.Equal(0, model.GenerationCalls);
            Assert.Equal("Lendo", (await first.Set<AssistenteAnaliseDocumental>().AsNoTracking().SingleAsync()).Estado);
        }
        await DrainAnalysis(db, storage, config, model, id);
        await using var verify = db.Context(); Assert.Equal("Concluida", (await verify.Set<AssistenteAnaliseDocumental>().SingleAsync()).Estado);
        Assert.Equal(model.GenerationCalls, await verify.AiUsageLedger.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Analise_IdentidadeECorrelacaoDoWorkerNaoDependemDeHttpContext()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); var model = new AnalysisModel();
        var (_, _, id) = await QueueAnalysis(db, storage, config, model);
        await using var context = db.Context(); var job = await context.Set<AssistenteAnaliseDocumental>().AsNoTracking().SingleAsync(j => j.Id == id);
        var identity = new AssistenteBackgroundIdentity();
        var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        var auth = new AssistenteExecutionAuthContext(new HttpAuthContext(accessor), identity);
        var correlation = new JuridicoAI.Api.Infrastructure.Http.HttpCorrelationContext(accessor, identity);
        var repository = new AssistenteWorkspaceRepository(context); var pieces = new PecaRepository(context);
        var documents = new AssistenteDocumentService(context, repository, pieces, storage, auth, db.Access, new Transparency(), Options.Create(config));
        var service = new AssistenteDocumentAnalysisService(context, repository, pieces, auth, db.Access, new Transparency(), documents, Options.Create(config), new Monitor(db.Settings), identity, correlation);
        await service.BindWorkerAsync(job, default); await service.ValidateAsync(job, default);
        Assert.Null(accessor.HttpContext); Assert.Equal(job.UserId, auth.UserId); Assert.Equal(job.OrganizationId, auth.OrganizationId);
        Assert.Equal(job.CorrelationId, correlation.CorrelationId); Assert.Equal(Guid.Empty, auth.SessionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Analise_LedgerDuravelNaoSilenciaFalhaDePersistencia(bool durable)
    {
        var recorder = new AssistenteUsageRecorder(new AnalysisBrokenLedger(), new Monitor(new AssistenteIaSettings()), NullLogger<AssistenteUsageRecorder>.Instance);
        var record = new AssistenteUsageRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            AssistenteModo.Perguntar, null, "OpenAI", "fixture", "analise-documental-v1", 1, 100, 0, 10, "succeeded", null, 1, durable);
        if (durable) await Assert.ThrowsAsync<IOException>(() => recorder.RegistrarAsync(record, default));
        else await recorder.RegistrarAsync(record, default);
    }
    private sealed class AnalysisBrokenLedger : IAiUsageLedgerRepository
    { public Task AdicionarAsync(AiUsageLedgerEntry entry, CancellationToken ct) => Task.FromException(new IOException("Falha sintética de persistência.")); }

    [ReviewPostgresFact]
    public async Task Analise_NovoPedidoExplicitoReaproveitaLotesDoPedidoLimitadoSemRepetirLeituraConfirmada()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var config = AnalysisSettings(); config.MaxAnalysisCalls = 3;
        var model = new AnalysisModel(); var (conversation, _, oldId) = await QueueAnalysis(db, storage, config, model, large: true);
        await DrainAnalysis(db, storage, config, model, oldId);
        config.MaxAnalysisCalls = 12; Guid nextId; int inherited;
        await using (var scope = db.Context())
        {
            var suite = AnalysisServices(db, scope, storage, config, model);
            var next = await suite.Chat.IniciarAsync(conversation, Free("Conclua a análise de todos os documentos com o trabalho já salvo."), default);
            var queued = await Assert.ThrowsAsync<AssistenteAnalysisQueuedException>(() => suite.Chat.CriarFerramentasDocumentais(next, (_, _) => Task.CompletedTask)!
                .ExecuteAsync("analisar_fontes", """{"coberturaPretendida":"Todos"}""", default));
            nextId = queued.Analysis.Id;
            var cached = await scope.Set<AssistenteAnaliseLote>().AsNoTracking().Where(b => b.AnaliseId == nextId && b.Estado == "Concluido").ToArrayAsync();
            inherited = cached.Length; Assert.True(inherited > 0);
            Assert.All(cached, b => { Assert.Equal(0, b.Chamadas); Assert.Equal(0, b.TokensReservados); });
        }
        await DrainAnalysis(db, storage, config, model, nextId);
        await using var verify = db.Context(); Assert.Equal("Concluida", (await verify.Set<AssistenteAnaliseDocumental>().SingleAsync(j => j.Id == nextId)).Estado);
        var newBatches = await verify.Set<AssistenteAnaliseLote>().Where(b => b.AnaliseId == nextId && b.Tipo == "Leitura").ToArrayAsync();
        Assert.Equal(newBatches.Length - inherited, await verify.AiUsageLedger.CountAsync(e => newBatches.Select(b => b.Id).Contains(e.SolicitacaoId)));
        Assert.Equal(4, await verify.AssistenteMensagens.CountAsync()); Assert.Equal(1, (await verify.Pecas.AsNoTracking().SingleAsync()).Versao);
    }

    private static AssistenteDocumentSettings AnalysisSettings() => new() { Enabled = true, RetrievalEnabled = true, AnalysisEnabled = true, AnalysisSummaryCharacters = 2000 };
    private static async Task<(Guid Conversation, AssistenteChatInicio Start, Guid Analysis)> QueueAnalysis(Database db,
        DocumentMemoryStorage storage, AssistenteDocumentSettings config, AnalysisModel model, bool large = false, bool partial = false)
    {
        db.Settings.Model = "fixture-analysis"; db.Settings.ApiKey = "synthetic"; db.Settings.Enabled = true;
        await using var context = db.Context();
        context.Add(new Membership { Id = Guid.NewGuid(), UserId = db.UserId, OrganizationId = db.OrgId, Role = MembershipRoleEnum.Owner, DataCriacao = DateTime.UtcNow, DataAtualizacao = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var suite = AnalysisServices(db, context, storage, config, model);
        var text = partial ? "Beatriz pagou R$ 876,54.\n\nTrecho \uFFFD duvidoso." : large ? string.Concat(Enumerable.Repeat("Beatriz pagou R$ 876,54 conforme o contrato. ", 2600)) : "Beatriz pagou R$ 876,54 conforme o contrato.";
        var source = await Upload(suite.Documents, conversation.Id, "contrato.txt", text);
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        var start = await suite.Chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Analise todos os documentos e melhore o primeiro parágrafo.", 1, AnexoIds: [source.Id]).Solicitar(), default);
        var tools = suite.Chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        var queued = await Assert.ThrowsAsync<AssistenteAnalysisQueuedException>(() => tools.ExecuteAsync("analisar_fontes", """{"coberturaPretendida":"Todos"}""", default));
        return (conversation.Id, start, queued.Analysis.Id);
    }
    private static async Task DrainAnalysis(Database db, DocumentMemoryStorage storage, AssistenteDocumentSettings config, AnalysisModel model, Guid id)
    {
        for (var i = 0; i < 80; i++)
        {
            await using var context = db.Context();
            var state = (await context.Set<AssistenteAnaliseDocumental>().AsNoTracking().SingleAsync(j => j.Id == id)).Estado;
            if (!AssistenteDocumentAnalysisService.Active(state)) return;
            Assert.True(await AnalysisServices(db, context, storage, config, model).Processor.ProcessNextAsync(default));
        }
        Assert.Fail("Análise não atingiu estado terminal.");
    }
    private static (AssistenteChatService Chat, AssistenteDocumentService Documents, AssistenteDocumentAnalysisService Analyses, AssistenteDocumentAnalysisProcessor Processor)
        AnalysisServices(Database db, JuridicoAiDbContext context, DocumentMemoryStorage storage, AssistenteDocumentSettings config, AnalysisModel model)
    {
        var repository = new AssistenteWorkspaceRepository(context); var pieces = new PecaRepository(context);
        var auth = new TestAuthContext(db.OrgId, db.UserId); var monitor = new Monitor(db.Settings); var docOptions = Options.Create(config);
        var documents = new AssistenteDocumentService(context, repository, pieces, storage, auth, db.Access, new Transparency(), docOptions);
        var analyses = new AssistenteDocumentAnalysisService(context, repository, pieces, auth, db.Access, new Transparency(), documents, docOptions, monitor, new AssistenteBackgroundIdentity());
        var chat = new AssistenteChatService(repository, pieces, auth, db.Access, new Transparency(), monitor, documents, analysis: analyses);
        var provider = new OpenAiAssistenteIaProvider(new HttpClient(new MemoryHandler(model.Send)) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(db.Settings));
        var limiter = new AnalysisLimiter(model);
        var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(context), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
        var generator = new AssistenteIaApplicationService(pieces, new CasoRepository(context), auth, provider, provider,
            new AssistenteContextBuilder(Options.Create(db.Settings)), new AssistentePromptPolicy(), new AssistenteRequestRegistry(), db.Access, new Transparency(), limiter, usage,
            new AnalysisCorrelation(), monitor, NullLogger<AssistenteIaApplicationService>.Instance, docOptions);
        return (chat, documents, analyses, new(context, analyses, documents, chat, generator, provider, provider, limiter, usage, docOptions, monitor));
    }
    private sealed class AnalysisCorrelation : ICorrelationContext { public Guid CorrelationId { get; } = Guid.NewGuid(); }
    private sealed class AnalysisLimiter(AnalysisModel model) : IAssistenteUsageLimiter
    {
        public ValueTask<IAsyncDisposable> AdquirirAsync(Guid organizationId, Guid userId, CancellationToken ct)
        {
            if (model.ThrottleOnce) { model.ThrottleOnce = false; throw new AssistenteIaException(429, "AI_USER_CONCURRENCY_LIMITED", "Aguarde.", true, retryAfter: TimeSpan.Zero); }
            return ValueTask.FromResult<IAsyncDisposable>(new Lease());
        }
        private sealed class Lease : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }
    private sealed class AnalysisModel
    {
        public Func<Task>? BeforeGeneration; public bool ThrottleOnce;
        public int GenerationCalls; public bool InvalidCitation; public string FinalScope = "Todos";
        public async Task<HttpResponseMessage> Send(HttpRequestMessage message)
        {
            if (message.RequestUri!.AbsolutePath.EndsWith("input_tokens")) return new(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":100}") };
            GenerationCalls++;
            if (BeforeGeneration is not null) await BeforeGeneration();
            using var json = JsonDocument.Parse(await message.Content!.ReadAsStringAsync()); var body = json.RootElement;
            if (body.TryGetProperty("tool_choice", out var choice) && choice.ValueKind == JsonValueKind.Object)
            {
                var name = choice.GetProperty("name").GetString(); Assert.Equal("registrar_sintese_documental", name);
                using var input = JsonDocument.Parse(body.GetProperty("input")[0].GetProperty("content").GetString()!);
                var type = input.RootElement.GetProperty("tipo").GetString(); var contents = input.RootElement.GetProperty("entradas").GetProperty("conteudos");
                Guid id; string quote;
                if (type == "Leitura") { id = contents[0].GetProperty("trechoId").GetGuid(); quote = contents[0].GetProperty("texto").GetString()![..Math.Min(90, contents[0].GetProperty("texto").GetString()!.Length)]; }
                else { var origin = contents[0][0].GetProperty("origens")[0]; id = origin.GetProperty("trechoId").GetGuid(); quote = origin.GetProperty("citacao").GetString()!; }
                if (InvalidCitation) quote = "Citação que não aparece no lote.";
                var args = JsonSerializer.Serialize(new { registros = new[] { new { categoria = "Valor", texto = "Beatriz pagou R$ 876,54. Informação preservada na síntese seletiva.", origens = new[] { new { trechoId = id, citacao = quote } } } } });
                return MemoryStream(null, [new { type = "function_call", call_id = Guid.NewGuid().ToString(), name, arguments = args }]);
            }
            var final = JsonSerializer.Serialize(new { titulo = "Alteração preparada", texto = "Proposta baseada na análise documental.", resumo = "", intencao = "EditarPeca", alertas = Array.Empty<object>(),
                grupos = new[] { new { explicacao = "Ajustar primeiro parágrafo", operacoes = new[] { new { startIndex = 0, deleteCount = 1, novosBlocos = new[] { AssistentePatchCompilerTests.Paragraph("Beatriz pagou R$ 876,54.") } } } } },
                substituirPropostaReferenciada = false, coberturaPretendida = FinalScope });
            return MemoryStream(final, []);
        }
    }
}
