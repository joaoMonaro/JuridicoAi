using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task ContextoLivre_PadraoUsaAnexoVinculadoSemCatalogarOuProcessarMateriaisDoCaso()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = RetrievalDocuments(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var piece = await context.Pecas.SingleAsync();
        await context.Casos.ExecuteUpdateAsync(s => s.SetProperty(c => c.Descricao, "DADO_ANTIGO_DO_CASO"));
        context.Add(new ContextoCaso { Id = Guid.NewGuid(), CasoId = piece.CasoId,
            Tipo = TipoContextoCasoEnum.Observacao, Conteudo = "CONTEXTO_ANTIGO_DO_CASO",
            DataCriacao = DateTime.UtcNow, DataAtualizacao = DateTime.UtcNow });
        context.Add(new DocumentoCaso { Id = Guid.NewGuid(), CasoId = piece.CasoId,
            NomeOriginal = "DOCUMENTO_ANTIGO_DO_CASO.txt", NomeArmazenado = "antigo.txt", StorageId = "case/antigo.txt",
            MimeType = "text/plain", Tipo = "Outro", TamanhoBytes = 10, DataUpload = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var linked = await Upload(docs, conversation.Id, "ANEXO_DA_CONVERSA.txt", "Relato fornecido nesta conversa.");
        await using (var processing = db.Context()) Assert.True(await Processor(processing, storage).ProcessNextAsync(default));
        var chat = RetrievalChat(db, context, docs);
        var start = await chat.IniciarAsync(conversation.Id,
            new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Prepare uma proposta com os anexos.", 1, AnexoIds: [linked.Id]).Solicitar(), default);
        Assert.Contains("ANEXO_DA_CONVERSA", start.Contexto);
        Assert.DoesNotContain("DADO_ANTIGO_DO_CASO", start.Contexto);
        Assert.DoesNotContain("CONTEXTO_ANTIGO_DO_CASO", start.Contexto);
        Assert.DoesNotContain("DOCUMENTO_ANTIGO_DO_CASO", start.Contexto);
        Assert.Equal(linked.Id, Assert.Single(await docs.ListAsync(conversation.Id, default)).Id);
        Assert.Equal(linked.Id, Assert.Single(await docs.CaptureSourcesAsync(conversation.Id, [], default)).Id);
        Assert.False(await context.AssistenteDocumentos.AnyAsync(d => d.DocumentoCasoId != null));
    }

    [ReviewPostgresFact]
    public async Task ContextoLivre_ConsultaExplicitaDeDadosFuncionaSemDocumentosENaoMudaIdempotencia()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        await context.Casos.ExecuteUpdateAsync(s => s.SetProperty(c => c.Descricao, "INFORMACAO_CADASTRADA"));
        var docs = new AssistenteDocumentService(context, new AssistenteWorkspaceRepository(context), new PecaRepository(context),
            new DocumentMemoryStorage(), new TestAuthContext(db.OrgId, db.UserId), db.Access, new Transparency(),
            Options.Create(new AssistenteDocumentSettings { Enabled = false }));
        var chat = RetrievalChat(db, context, docs);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        const string message = "Consulte os dados cadastrados no Caso.";
        var request = new EnviarInteracaoLivreRequest(Guid.NewGuid(), message, 1).Solicitar();
        var start = await chat.IniciarAsync(conversation.Id, request, default);
        Assert.DoesNotContain("INFORMACAO_CADASTRADA", start.Contexto);
        var originalRequest = start.Pedido.Solicitacao!.RootElement.GetRawText();
        var originalHash = start.Pedido.RequestSha256;
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        var result = await tools.ExecuteAsync("consultar_caso", JsonSerializer.Serialize(new {
            incluirDados = true, incluirDocumentos = false, trechoDoPedido = message }), default);
        Assert.Contains("INFORMACAO_CADASTRADA", result);
        var saved = await context.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == start.Pedido.Id);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(originalRequest).RootElement, saved.Solicitacao!.RootElement));
        Assert.Equal(originalHash, saved.RequestSha256);
        Assert.Contains("DadosCaso", saved.FontesDocumentais!.RootElement.GetRawText());
        Assert.True(await docs.ManifestValidAsync(saved.FontesDocumentais, conversation.Id, start.CasoId!.Value, default));
        Assert.False((await chat.IniciarAsync(conversation.Id, request, default)).Novo);
        Assert.Equal(2, await context.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task ContextoLivre_ContextoCadastradoRevogadoInvalidaConsultaEImpedePublicacao()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var docs = RetrievalDocuments(db, context, new DocumentMemoryStorage());
        var piece = await context.Pecas.SingleAsync(); var id = Guid.NewGuid();
        context.Add(new ContextoCaso { Id = id, CasoId = piece.CasoId, Tipo = TipoContextoCasoEnum.Observacao,
            Conteudo = "INFORMACAO_REVOGAVEL", DataCriacao = DateTime.UtcNow, DataAtualizacao = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var chat = RetrievalChat(db, context, docs);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        const string message = "Consulte os contextos cadastrados no Caso.";
        var start = await chat.IniciarAsync(conversation.Id,
            new EnviarInteracaoLivreRequest(Guid.NewGuid(), message, 1).Solicitar(), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        var output = await tools.ExecuteAsync("consultar_caso", JsonSerializer.Serialize(new {
            incluirDados = true, incluirDocumentos = false, trechoDoPedido = message }), default);
        Assert.Contains("INFORMACAO_REVOGAVEL", output);
        await context.ContextosCaso.Where(c => c.Id == id).ExecuteDeleteAsync();
        var saved = await context.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == start.Pedido.Id);
        Assert.False(await docs.ManifestValidAsync(saved.FontesDocumentais, conversation.Id, piece.CasoId, default));
        var blocked = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            chat.ConcluirAsync(start, ChatResult(start.Pedido.Id, "Resposta derivada da fonte revogada."), default));
        Assert.Equal("AI_DOCUMENT_SOURCE_CHANGED", blocked.Code);
        Assert.Null((await context.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == start.Resposta.Id)).Resultado);
        Assert.Equal(1, (await context.Pecas.AsNoTracking().SingleAsync()).Versao);
    }
}
