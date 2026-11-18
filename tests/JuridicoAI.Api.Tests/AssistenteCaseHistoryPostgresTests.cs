using System.Net;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task ContextoLivre_HistoricoAntigoDoCasoContinuaLegivelSemReutilizarFatosAutomaticos()
        => await VerifyCaseHistoryAsync(explicitRequest: false);

    [ReviewPostgresFact]
    public async Task ContextoLivre_HistoricoDeConsultaExplicitaReutilizaFontesValidas()
        => await VerifyCaseHistoryAsync(explicitRequest: true);

    [ReviewPostgresFact]
    public async Task ContextoLivre_GeracaoDuravelPreservaConsultaExplicitaEFontesAposNovoWorker()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage();
        var model = new GenerationModel { ConsultCaseFirst = true };
        const string message = "Consulte os dados e documentos antigos do Caso e elabore a minuta.";
        var (conversation, queued) = await QueueGeneration(db, storage, model, message: message);
        var originalHash = queued.Pedido.RequestSha256;
        Guid caseDocumentId;
        await using (var setup = db.Context())
        {
            var piece = await setup.Pecas.SingleAsync();
            await setup.Casos.ExecuteUpdateAsync(s => s.SetProperty(c => c.Descricao, "CASE_DATA_MARKER"));
            caseDocumentId = Guid.NewGuid(); const string key = "case/durable.txt";
            storage.Files[key] = Encoding.UTF8.GetBytes("Fonte documental do Caso.");
            setup.Add(new DocumentoCaso { Id = caseDocumentId, CasoId = piece.CasoId,
                NomeOriginal = "durable-case.txt", NomeArmazenado = "durable-case.txt", StorageId = key,
                MimeType = "text/plain", Tipo = "Outro", TamanhoBytes = storage.Files[key].Length, DataUpload = DateTime.UtcNow });
            await setup.SaveChangesAsync();
            var job = await setup.Set<AssistenteGeracao>().AsNoTracking().SingleAsync();
            Assert.Equal(0, job.FontesIds.RootElement.GetArrayLength());
            await GenerationServices(db, setup, storage, model).Documents.SynchronizeAsync(conversation, default, incluirFontesCaso: true);
        }
        await using (var extraction = db.Context()) Assert.True(await Processor(extraction, storage).ProcessNextAsync(default));
        await using (var firstWorker = db.Context()) Assert.True(await GenerationServices(db, firstWorker, storage, model).Processor.ProcessNextAsync(default));
        await using (var restarted = db.Context())
        {
            var job = await restarted.Set<AssistenteGeracao>().AsNoTracking().SingleAsync();
            Assert.Equal("Redigindo", job.Estado);
            Assert.Single(AssistenteGenerationService.Read<AssistenteSourceSnapshot[]>(job.FontesIds));
            var saved = AssistenteGenerationService.Read<AssistenteGenerationContext>(job.Contexto!);
            Assert.Contains("CASE_DATA_MARKER", saved.Evidencias);
            Assert.Equal("DocumentoCaso", Assert.Single(saved.Fontes!).Origem);
            var user = await restarted.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == queued.Pedido.Id);
            Assert.Equal(originalHash, user.RequestSha256);
            Assert.Contains("consultaExplicitaCaso", user.FontesDocumentais!.RootElement.GetRawText());
        }
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context();
        var complete = await verify.Set<AssistenteGeracao>().AsNoTracking().SingleAsync();
        Assert.Equal("Concluida", complete.Estado); Assert.Equal(1, model.CaseConsultations);
        Assert.True(model.SawCaseEvidenceInSection);
        await verify.DocumentosCaso.Where(d => d.Id == caseDocumentId).ExecuteDeleteAsync();
        var error = await Assert.ThrowsAsync<AssistenteIaException>(() => GenerationServices(db, verify, storage, model).Generations.ValidateAsync(complete, default));
        Assert.Equal("AI_DOCUMENT_SOURCE_CHANGED", error.Code);
    }

    [ReviewPostgresFact]
    public async Task ContextoLivre_ProviderConcluiConsultaSoDeDadosSemEscopoDocumental()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var docs = new AssistenteDocumentService(context, new AssistenteWorkspaceRepository(context), new PecaRepository(context),
            new DocumentMemoryStorage(), new TestAuthContext(db.OrgId, db.UserId), db.Access, new Transparency(),
            Options.Create(new AssistenteDocumentSettings { Enabled = false }));
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var chat = RetrievalChat(db, context, docs);
        const string message = "Consulte os dados cadastrados no Caso.";
        var start = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), message, 1).Solicitar(), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        Assert.False(tools.HasDocumentTools); Assert.Null(tools.SetScope);
        var calls = 0;
        var provider = new OpenAiAssistenteIaProvider(new HttpClient(new MemoryHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":100}") });
            calls++;
            return Task.FromResult(calls == 1
                ? MemoryStream(null, [new { type = "function_call", name = "consultar_caso", call_id = "case_data",
                    arguments = JsonSerializer.Serialize(new { incluirDados = true, incluirDocumentos = false, trechoDoPedido = message }) }])
                : MemoryStream("""{"titulo":"Dados do Caso","texto":"Consulta concluída.","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false}""", []));
        })) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(db.Settings));
        var result = await provider.GerarAsync(new AssistenteIaProviderRequest(new AssistentePromptPolicy().CriarInstrucoesLivres(false, false),
            start.Contexto, AssistenteModo.Perguntar, [AssistenteOperacao.ResponderNoPainel], 1000,
            Conversacional: true, FerramentasDocumentais: tools), default);
        Assert.Equal("Consulta concluída.", result.Texto); Assert.Equal(2, calls);
        Assert.Null(result.CoberturaPretendida);
    }

    private static async Task VerifyCaseHistoryAsync(bool explicitRequest)
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = RetrievalDocuments(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var piece = await context.Pecas.SingleAsync();
        const string storageId = "case/historico.txt";
        storage.Files[storageId] = Encoding.UTF8.GetBytes("Fonte antiga do Caso.");
        context.Add(new DocumentoCaso { Id = Guid.NewGuid(), CasoId = piece.CasoId,
            NomeOriginal = "historico-caso.txt", NomeArmazenado = "historico-caso.txt", StorageId = storageId,
            MimeType = "text/plain", Tipo = "Outro", TamanhoBytes = storage.Files[storageId].Length, DataUpload = DateTime.UtcNow });
        await context.SaveChangesAsync();
        await docs.SynchronizeAsync(conversation.Id, default, incluirFontesCaso: true);
        await using (var processing = db.Context()) Assert.True(await Processor(processing, storage).ProcessNextAsync(default));
        var chat = RetrievalChat(db, context, docs);
        const string request = "Consulte os documentos antigos do Caso.";
        var first = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), request, 1).Solicitar(), default);
        if (explicitRequest)
        {
            var tools = chat.CriarFerramentasDocumentais(first, (_, _) => Task.CompletedTask)!;
            await tools.ExecuteAsync("consultar_caso", JsonSerializer.Serialize(new {
                incluirDados = false, incluirDocumentos = true, trechoDoPedido = request }), default);
        }
        else
        {
            // Reproduce persisted history from the former automatic Case catalog.
            var original = await docs.ContextAsync(await context.AssistenteConversas.SingleAsync(), piece, [], default,
                usarRecuperacao: true, incluirFontesCaso: true);
            first.Pedido.FontesDocumentais = JsonDocument.Parse(original.Manifesto);
            await context.AssistenteMensagens.Where(m => m.Id == first.Pedido.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.FontesDocumentais, first.Pedido.FontesDocumentais));
        }
        const string derivedFact = "FATO_DERIVADO_DO_MATERIAL_ANTIGO";
        await chat.ConcluirAsync(first, ChatResult(first.Pedido.Id, derivedFact), default);
        Assert.Contains(derivedFact, (await context.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == first.Resposta.Id)).Texto);
        var next = await chat.IniciarAsync(conversation.Id,
            new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Continue a conversa.", 1).Solicitar(), default);
        if (explicitRequest) Assert.Contains(derivedFact, next.Contexto);
        else Assert.DoesNotContain(derivedFact, next.Contexto);
    }
}
