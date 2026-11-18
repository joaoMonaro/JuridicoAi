using System.Text.Json;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task Recuperacao_DocumentoExtensoUsaCatalogoEBuscaSemTruncarOuRepetirCobertura()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = RetrievalDocuments(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "extenso.txt", string.Concat(Enumerable.Repeat("Contrato regular. ", 6000)) + "\n\nBeatriz pagou R$ 876,54.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        var chat = RetrievalChat(db, context, docs);
        var start = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Qual o valor pago por Beatriz?", 1, AnexoIds: [source.Id]).Solicitar(), default);
        Assert.DoesNotContain("876,54", start.Contexto); Assert.Contains("CATALOGO_FONTES_DOCUMENTAIS_JSON", start.Contexto);
        var events = new List<AssistenteCoberturaDocumentalDto>();
        var tools = chat.CriarFerramentasDocumentais(start, (coverage, _) => { events.Add(coverage); return Task.CompletedTask; })!;
        var found = await tools.ExecuteAsync("buscar_fontes", """{"consulta":"Beatriz","limite":1}""", default);
        Assert.Contains("876,54", found); Assert.Equal(1, tools.Coverage.Fontes.Single().TrechosConsultados);
        Assert.True(tools.Coverage.Fontes.Single().TrechosElegiveis > 20);
        await tools.ExecuteAsync("buscar_fontes", """{"consulta":"Beatriz","limite":1}""", default);
        Assert.Equal(1, tools.Coverage.Fontes.Single().TrechosConsultados);
        Assert.Equal(2, events.Count);
        var restored = AssistenteChatService.Dto(await context.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == start.Resposta.Id));
        Assert.Equal(1, restored.CoberturaDocumental!.Fontes.Single().TrechosConsultados);
        Assert.Equal("Parcial", restored.CoberturaDocumental.Estado); Assert.Equal(1, (await context.Pecas.SingleAsync()).Versao);
    }

    [ReviewPostgresFact]
    public async Task Recuperacao_TodosMantemLacunaDeExtracaoMesmoAposConsultarTodosOsTrechosElegiveis()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = RetrievalDocuments(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "parcial.txt", "Texto confiável.\n\nTrecho \uFFFD duvidoso.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        var chat = RetrievalChat(db, context, docs);
        var start = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Considere todos os documentos.", 1, AnexoIds: [source.Id]).Solicitar(), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        await tools.ExecuteAsync("listar_fontes", """{"aposIndice":0,"limite":25,"coberturaPretendida":"Todos"}""", default);
        var returned = await tools.ExecuteAsync("ler_fonte", JsonSerializer.Serialize(new { fonteId = source.Id, aposOrdem = -1, limite = 12 }), default);
        Assert.DoesNotContain("duvidoso", returned);
        tools.SetScope!("Relevantes"); Assert.Equal("Todos", tools.Coverage.EscopoSolicitado);
        var coverage = tools.Coverage; var file = coverage.Fontes.Single();
        Assert.Equal(file.TrechosElegiveis, file.TrechosConsultados); Assert.Equal(1, file.TrechosExcluidos); Assert.Equal("Parcial", coverage.Estado);
    }

    [ReviewPostgresFact]
    public async Task Recuperacao_AcessoEQualidadeValidosNaoPermitemLerFonteForaDoSnapshot()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = RetrievalDocuments(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var first = await Upload(docs, conversation.Id, "primeiro.txt", "Beatriz tem contrato.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        var chat = RetrievalChat(db, context, docs);
        var start = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Analise o contrato.", 1, AnexoIds: [first.Id]).Solicitar(), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        var late = await Upload(docs, conversation.Id, "posterior.txt", "Beatriz Beatriz Beatriz: fonte fora do snapshot.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        await context.AssistenteDocumentos.Where(d => d.Id == late.Id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Vinculado, true));
        var denied = await Assert.ThrowsAsync<AssistenteIaException>(() => tools.ExecuteAsync("ler_fonte", JsonSerializer.Serialize(new { fonteId = late.Id, aposOrdem = -1, limite = 1 }), default));
        Assert.Equal(404, denied.StatusCode);
        var result = await tools.ExecuteAsync("buscar_fontes", """{"consulta":"Beatriz","limite":1}""", default);
        Assert.DoesNotContain("fora do snapshot", result); Assert.Contains(first.Id.ToString(), result);
        await docs.RetryAsync(conversation.Id, first.Id, default);
        Assert.Equal("AI_DOCUMENT_SOURCE_CHANGED", (await Assert.ThrowsAsync<AssistenteIaException>(() => tools.ExecuteAsync("buscar_fontes", """{"consulta":"Beatriz","limite":1}""", default))).Code);
    }

    [ReviewPostgresFact]
    public async Task Recuperacao_CancelamentoMantemCheckpointESuspendeNovasConsultas()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = RetrievalDocuments(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "curto.txt", "Beatriz tem contrato.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        var chat = RetrievalChat(db, context, docs);
        var start = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Leia o contrato.", 1, AnexoIds: [source.Id]).Solicitar(), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        await tools.ExecuteAsync("ler_fonte", JsonSerializer.Serialize(new { fonteId = source.Id, aposOrdem = -1, limite = 1 }), default);
        Assert.Equal("Completa", tools.Coverage.Estado);
        await chat.FalharAsync(start, "AI_CANCELLED", true);
        Assert.Equal("AI_REQUEST_INTERRUPTED", (await Assert.ThrowsAsync<AssistenteIaException>(() => tools.ExecuteAsync("listar_fontes", """{"aposIndice":0,"limite":25,"coberturaPretendida":"Todos"}""", default))).Code);
        var response = await context.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == start.Resposta.Id);
        Assert.Equal("Cancelada", response.Estado); Assert.NotNull(response.CoberturaDocumental);
        Assert.Equal(1, AssistenteChatService.Dto(response).CoberturaDocumental!.Fontes.Single().TrechosConsultados);
    }

    [ReviewPostgresFact]
    public async Task Recuperacao_LeituraPorPaginaRetornaSomenteRegiaoSolicitada()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = RetrievalDocuments(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "paginas.txt", "Primeira região.\n\nSegunda região.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        await context.AssistenteDocumentoTrechos.Where(c => c.DocumentoId == source.Id && c.Ordem == 0).ExecuteUpdateAsync(s => s.SetProperty(c => c.Pagina, 1));
        await context.AssistenteDocumentoTrechos.Where(c => c.DocumentoId == source.Id && c.Ordem == 1).ExecuteUpdateAsync(s => s.SetProperty(c => c.Pagina, 2));
        var chat = RetrievalChat(db, context, docs);
        var start = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Leia a página 2.", 1, AnexoIds: [source.Id]).Solicitar(), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        var output = await tools.ExecuteAsync("ler_fonte", JsonSerializer.Serialize(new { fonteId = source.Id, aposOrdem = -1, limite = 12, pagina = 2 }), default);
        Assert.Contains("Segunda", output); Assert.DoesNotContain("Primeira", output);
        Assert.Equal(1, tools.Coverage.Fontes.Single().TrechosConsultados);
    }

    private static AssistenteDocumentService RetrievalDocuments(Database db, JuridicoAiDbContext context, DocumentMemoryStorage storage) =>
        new(context, new AssistenteWorkspaceRepository(context), new PecaRepository(context), storage, new TestAuthContext(db.OrgId, db.UserId),
            db.Access, new Transparency(), Options.Create(new AssistenteDocumentSettings { Enabled = true, RetrievalEnabled = true }));
    private static AssistenteChatService RetrievalChat(Database db, JuridicoAiDbContext context, AssistenteDocumentService docs) =>
        new(new AssistenteWorkspaceRepository(context), new PecaRepository(context), new TestAuthContext(db.OrgId, db.UserId),
            db.Access, new Transparency(), new Monitor(db.Settings), docs);
}
