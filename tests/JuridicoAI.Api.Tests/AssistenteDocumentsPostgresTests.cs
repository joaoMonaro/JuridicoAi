using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Documents;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task Documentos_UploadIdempotentePrivadoNaoAlteraPeca()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var service = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var upload = Guid.NewGuid();
        var first = await Upload(service, conversation.Id, "contrato.txt", "Ana pagou R$ 123,45.", upload);
        var replay = await Upload(service, conversation.Id, "contrato.txt", "Ana pagou R$ 123,45.", upload);
        Assert.Equal(first.Id, replay.Id); Assert.Single(storage.Files);
        Assert.Equal(1, (await context.Pecas.SingleAsync()).Versao);
        var conflict = await Assert.ThrowsAsync<AssistenteIaException>(() => Upload(service, conversation.Id, "contrato.txt", "Outro texto", upload));
        Assert.Equal("AI_IDEMPOTENCY_MISMATCH", conflict.Code);
        var foreign = Documents(db, context, storage, Guid.NewGuid());
        Assert.Equal(404, (await Assert.ThrowsAsync<AssistenteIaException>(() => foreign.ListAsync(conversation.Id, default))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<AssistenteIaException>(() => foreign.DownloadAsync(conversation.Id, first.Id, default))).StatusCode);
    }

    [ReviewPostgresFact]
    public async Task Documentos_FilaPersisteLeituraEVinculaSomenteNoEnvio()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "fatos.txt", "Beatriz pagou R$ 123,45.\n\nO prazo venceu.");
        var request = new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Considere o documento.", 1, AnexoIds: [source.Id]).Solicitar();
        var chat = new AssistenteChatService(new AssistenteWorkspaceRepository(context), new PecaRepository(context),
            new TestAuthContext(db.OrgId, db.UserId), db.Access, new Transparency(), new Monitor(db.Settings), docs);
        var pending = await Assert.ThrowsAsync<AssistenteIaException>(() => chat.IniciarAsync(conversation.Id, request, default));
        Assert.Equal("AI_DOCUMENTS_PROCESSING", pending.Code); Assert.Empty(await context.AssistenteMensagens.ToArrayAsync());
        await using (var processing = db.Context()) Assert.True(await Processor(processing, storage).ProcessNextAsync(default));
        context.ChangeTracker.Clear();
        var state = Assert.Single(await docs.ListAsync(conversation.Id, default));
        Assert.Equal("Concluido", state.Estado); Assert.False(state.Vinculado); Assert.Equal(2, state.TrechosConfiaveis);
        var start = await chat.IniciarAsync(conversation.Id, request, default);
        Assert.Contains("Beatriz pagou", start.Contexto); Assert.Contains(JsonEncodedText.Encode("parágrafo 1").ToString(), start.Contexto);
        Assert.NotNull(start.Pedido.FontesDocumentais);
        Assert.True(Assert.Single(await docs.ListAsync(conversation.Id, default)).Vinculado);
        Assert.Equal(1, (await context.Pecas.SingleAsync()).Versao);
    }

    [ReviewPostgresFact]
    public async Task Documentos_TrechosDuvidososNuncaEntramNoContextoENemNoIndice()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "parcial.txt", "Texto legível.\n\nValor \uFFFD incorreto.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        context.ChangeTracker.Clear();
        var piece = await context.Pecas.SingleAsync(); var conv = await context.AssistenteConversas.SingleAsync();
        var assembled = await docs.ContextAsync(conv, piece, [source.Id], default);
        Assert.Contains(JsonEncodedText.Encode("Texto legível.").ToString(), assembled.Texto); Assert.DoesNotContain("incorreto", assembled.Texto);
        Assert.Single(assembled.Alertas); Assert.Contains("CHARACTER_MAPPING_INVALID", assembled.Texto);
        var bad = await context.AssistenteDocumentoTrechos.SingleAsync(c => c.Qualidade == "Duvidoso");
        Assert.Equal("", bad.Busca.ToString());
        Assert.Equal("Parcial", Assert.Single(await docs.ListAsync(conversation.Id, default)).Estado);
    }

    [ReviewPostgresFact]
    public async Task Documentos_RetomaLeaseExpiradoSemDuplicarOuUsarParcialAnterior()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "recuperar.txt", "Conteúdo íntegro.");
        var job = await context.AssistenteDocumentos.SingleAsync();
        job.Estado = "Processando"; job.LeaseAte = DateTime.UtcNow.AddMinutes(-1); job.LeaseToken = Guid.NewGuid(); job.Tentativas = 1;
        context.Add(new AssistenteDocumentoTrecho { Id = Guid.NewGuid(), OrganizationId = db.OrgId, DocumentoId = source.Id,
            Ordem = 0, Texto = "Checkpoint antigo", Qualidade = "Confiavel", Sha256 = new string('a', 64) }); await context.SaveChangesAsync();
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        context.ChangeTracker.Clear();
        Assert.Equal("Conteúdo íntegro.", (await context.AssistenteDocumentoTrechos.SingleAsync()).Texto);
        Assert.Equal(2, (await context.AssistenteDocumentos.SingleAsync()).Tentativas);
    }

    [ReviewPostgresFact]
    public async Task Documentos_RevogaFonteDoCasoEInvalidaHistoricoSemApagarConversa()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default); var piece = await context.Pecas.SingleAsync();
        var id = Guid.NewGuid(); var key = "case/ficticio.txt"; storage.Files[key] = Encoding.UTF8.GetBytes("Documento compartilhado.");
        context.Add(new DocumentoCaso { Id = id, CasoId = piece.CasoId, NomeOriginal = "prova.txt", NomeArmazenado = "prova.txt",
            StorageId = key, TamanhoBytes = storage.Files[key].Length, MimeType = "text/plain", Tipo = "Outro", DataUpload = DateTime.UtcNow }); await context.SaveChangesAsync();
        await docs.ListAsync(conversation.Id, default);
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        context.ChangeTracker.Clear();
        var conv = await context.AssistenteConversas.SingleAsync();
        var assembled = await docs.ContextAsync(conv, piece, [], default); using var manifest = JsonDocument.Parse(assembled.Manifesto);
        Assert.True(await docs.ManifestValidAsync(manifest, conv.Id, piece.CasoId, default));
        await context.DocumentosCaso.Where(d => d.Id == id).ExecuteDeleteAsync();
        Assert.False(await docs.ManifestValidAsync(manifest, conv.Id, piece.CasoId, default));
        Assert.Empty(await docs.ListAsync(conv.Id, default)); Assert.Single(await context.AssistenteConversas.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Documentos_ReprocessamentoInvalidaManifestoMesmoComMesmoArquivo()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "fato.txt", "Texto legível.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        context.ChangeTracker.Clear(); var piece = await context.Pecas.SingleAsync(); var conv = await context.AssistenteConversas.SingleAsync();
        var assembled = await docs.ContextAsync(conv, piece, [source.Id], default); using var manifest = JsonDocument.Parse(assembled.Manifesto);
        Assert.True(await docs.ManifestValidAsync(manifest, conv.Id, piece.CasoId, default));
        await docs.RetryAsync(conv.Id, source.Id, default);
        Assert.False(await docs.ManifestValidAsync(manifest, conv.Id, piece.CasoId, default));
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        context.ChangeTracker.Clear();
        Assert.False(await docs.ManifestValidAsync(manifest, conv.Id, piece.CasoId, default));
    }

    [ReviewPostgresFact]
    public async Task Documentos_BuscaFiltraQualidadeEConversaAntesDoLimite()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var own = await Upload(docs, conversation.Id, "próprio.txt", "Beatriz pagou o contrato.\n\nBeatriz \uFFFD incorreto.");
        var other = Guid.NewGuid(); var now = DateTime.UtcNow;
        context.Add(new User { Id = other, Nome = "Outra titular", Email = $"{other:N}@example.test", PasswordHash = "ficticio", DataCriacao = now, DataAtualizacao = now }); await context.SaveChangesAsync();
        var otherConversation = await db.Service(context, other).ObterOuCriarAsync(db.PecaId, default);
        var otherDocs = Documents(db, context, storage, other);
        var foreign = await Upload(otherDocs, otherConversation.Id, "alheio.txt", "Beatriz informação privada alheia.");
        await using (var processing = db.Context())
        { var processor = Processor(processing, storage); await processor.ProcessNextAsync(default); await processor.ProcessNextAsync(default); }
        context.ChangeTracker.Clear();
        var piece = await context.Pecas.SingleAsync();
        await docs.ContextAsync(await context.AssistenteConversas.SingleAsync(c => c.Id == conversation.Id), piece, [own.Id], default);
        await otherDocs.ContextAsync(await context.AssistenteConversas.SingleAsync(c => c.Id == otherConversation.Id), piece, [foreign.Id], default);
        var hit = Assert.Single(await docs.SearchAsync(conversation.Id, "Beatriz", 1, default));
        Assert.Equal(own.Id, hit.DocumentoId); Assert.DoesNotContain("incorreto", hit.Texto); Assert.DoesNotContain("alheia", hit.Texto);
        var denied = await Assert.ThrowsAsync<AssistenteIaException>(async () => await docs.ContextAsync(
            await context.AssistenteConversas.SingleAsync(c => c.Id == conversation.Id), piece, [foreign.Id], default));
        Assert.Equal(404, denied.StatusCode);
    }

    [ReviewPostgresFact]
    public async Task Documentos_LimitesNaoApagamOriginaisENaoTruncamContexto()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var settings = new AssistenteDocumentSettings { Enabled = true, MaxAttachments = 1 };
        var docs = new AssistenteDocumentService(context, new AssistenteWorkspaceRepository(context), new PecaRepository(context), storage,
            new TestAuthContext(db.OrgId, db.UserId), db.Access, new Transparency(), Options.Create(settings));
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "extenso.txt", string.Concat(Enumerable.Repeat("texto ", 11_000)));
        Assert.Equal(413, (await Assert.ThrowsAsync<AssistenteIaException>(() => Upload(docs, conversation.Id, "extra.txt", "Outro arquivo"))).StatusCode);
        Assert.Single(storage.Files);
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        context.ChangeTracker.Clear();
        var limit = await Assert.ThrowsAsync<AssistenteIaException>(async () => await docs.ContextAsync(
            await context.AssistenteConversas.SingleAsync(), await context.Pecas.SingleAsync(), [source.Id], default));
        Assert.Equal("AI_DOCUMENT_CONTEXT_TOO_LARGE", limit.Code);
        Assert.Single(storage.Files); Assert.Single(await context.AssistenteDocumentos.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Documentos_FonteRevogadaDuranteGeracaoImpedePublicacao()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "fonte.txt", "Conteúdo inicial.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        context.ChangeTracker.Clear();
        var chat = new AssistenteChatService(new AssistenteWorkspaceRepository(context), new PecaRepository(context),
            new TestAuthContext(db.OrgId, db.UserId), db.Access, new Transparency(), new Monitor(db.Settings), docs);
        var start = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Explique a fonte", 1, AnexoIds: [source.Id]).Solicitar(), default);
        await docs.RetryAsync(conversation.Id, source.Id, default);
        var output = new AssistenteSolicitacaoConcluidaDto(new(Guid.NewGuid(), AssistenteModo.Perguntar,
            AssistenteOperacao.ResponderNoPainel, "Resposta", "Rascunho baseado na fonte.", "", [], [],
            new(null, null, null, 0, null)), new(1, 1), "fixture");
        var conflict = await Assert.ThrowsAsync<AssistenteIaException>(() => chat.ConcluirAsync(start, output, default));
        Assert.Equal("AI_DOCUMENT_SOURCE_CHANGED", conflict.Code);
        Assert.Null((await context.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.RespostaAId == start.Pedido.Id)).Resultado);
        Assert.Equal(1, (await context.Pecas.SingleAsync()).Versao);
    }

    [ReviewPostgresFact]
    public async Task Documentos_WorkerAntigoNaoPublicaDepoisDePerderLease()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "bloqueado.txt", "Não deve ser publicado.");
        var gate = new DocumentGateStorage(storage);
        await using var processing = db.Context();
        var running = Processor(processing, gate).ProcessNextAsync(default);
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var newFence = Guid.NewGuid();
        await context.AssistenteDocumentos.Where(d => d.Id == source.Id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.LeaseToken, newFence).SetProperty(d => d.LeaseAte, DateTime.UtcNow.AddMinutes(1)));
        gate.Release.SetResult(); await running;
        Assert.Empty(await context.AssistenteDocumentoTrechos.ToArrayAsync());
        Assert.Equal(newFence, (await context.AssistenteDocumentos.AsNoTracking().SingleAsync()).LeaseToken);
    }

    [ReviewPostgresFact]
    public async Task Documentos_PrimeiroEnvioSemListagemAnteriorPreservaFilaDoCaso()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default); var piece = await context.Pecas.SingleAsync();
        storage.Files["case/auto.txt"] = Encoding.UTF8.GetBytes("Fonte automática do Caso.");
        context.Add(new DocumentoCaso { Id = Guid.NewGuid(), CasoId = piece.CasoId, NomeOriginal = "auto.txt", NomeArmazenado = "auto.txt",
            StorageId = "case/auto.txt", TamanhoBytes = storage.Files["case/auto.txt"].Length, MimeType = "text/plain", Tipo = "Outro", DataUpload = DateTime.UtcNow }); await context.SaveChangesAsync();
        var chat = new AssistenteChatService(new AssistenteWorkspaceRepository(context), new PecaRepository(context),
            new TestAuthContext(db.OrgId, db.UserId), db.Access, new Transparency(), new Monitor(db.Settings), docs);
        var request = new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Leia a fonte do Caso.", 1).Solicitar();
        Assert.Equal("AI_DOCUMENTS_PROCESSING", (await Assert.ThrowsAsync<AssistenteIaException>(() => chat.IniciarAsync(conversation.Id, request, default))).Code);
        Assert.Single(await context.AssistenteDocumentos.ToArrayAsync()); Assert.Empty(await context.AssistenteMensagens.ToArrayAsync());
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        Assert.Contains(JsonEncodedText.Encode("Fonte automática do Caso.").ToString(), (await chat.IniciarAsync(conversation.Id, request, default)).Contexto);
    }

    [ReviewPostgresFact]
    public async Task Documentos_RevalidacaoNaAprovacaoBloqueiaFonteReprocessada()
    {
        await using var db = await Database.Create(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var docs = Documents(db, context, storage);
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var source = await Upload(docs, conversation.Id, "base.txt", "Fonte da proposta.");
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        var chat = new AssistenteChatService(new AssistenteWorkspaceRepository(context), new PecaRepository(context),
            new TestAuthContext(db.OrgId, db.UserId), db.Access, new Transparency(), new Monitor(db.Settings), docs);
        var start = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Melhore a redação", 1, AnexoIds: [source.Id]).Solicitar(), default);
        var result = await chat.ConcluirLivreAsync(start, AutomaticResult(start.Pedido.Id, Request().Grupos), default);
        await docs.RetryAsync(conversation.Id, source.Id, default);
        var workspace = new AssistenteWorkspaceService(new AssistenteWorkspaceRepository(context), new PecaRepository(context),
            new TestAuthContext(db.OrgId, db.UserId), db.Access, new Transparency(), new Monitor(db.Settings), new AssistentePatchCompiler(), docs);
        var proposal = result.Proposta!;
        var request = new AplicarGruposRequest(Guid.NewGuid(), proposal.Revisao, proposal.VersaoDecisoes, proposal.VersaoPecaAplicavel,
            proposal.Grupos.Select(g => new AprovarGrupoRequest(g.Id, g.PreviewSha256)).ToArray());
        Assert.Equal("AI_DOCUMENT_SOURCE_CHANGED", (await Assert.ThrowsAsync<AssistenteIaException>(() => workspace.AplicarAsync(proposal.Id, request, default))).Code);
        Assert.Equal(1, (await context.Pecas.SingleAsync()).Versao); Assert.Empty(await context.AssistenteRecibos.ToArrayAsync());
        Assert.Equal("Desatualizada", (await workspace.PropostaAsync(proposal.Id, default)).Estado);
        await using (var processing = db.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        var followup = await chat.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Ajuste essa proposta.", 1,
            PropostaReferenciadaId: proposal.Id).Solicitar(), default);
        Assert.DoesNotContain("Primeiro revisado", followup.Contexto);
        Assert.Contains("\"propostaReferenciadaOmitidaPorFonteInelegivel\":true", followup.Contexto);
    }

    private sealed class DocumentGateStorage(IFileStorage storage) : IFileStorage
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<Stream> GetAsync(string id, CancellationToken ct)
        { Started.SetResult(); await Release.Task.WaitAsync(ct); return await storage.GetAsync(id, ct); }
        public Task SaveAsync(string id, Stream content, CancellationToken ct) => storage.SaveAsync(id, content, ct);
        public Task DeleteAsync(string id, CancellationToken ct) => storage.DeleteAsync(id, ct);
    }
    private static AssistenteDocumentService Documents(Database db, JuridicoAiDbContext context, DocumentMemoryStorage storage, Guid? user = null) =>
        new(context, new AssistenteWorkspaceRepository(context), new PecaRepository(context), storage, new TestAuthContext(db.OrgId, user ?? db.UserId),
            db.Access, new Transparency(), Options.Create(new AssistenteDocumentSettings { Enabled = true }));
    private static AssistenteDocumentProcessor Processor(JuridicoAiDbContext context, IFileStorage storage) =>
        new(context, storage, new LocalDocumentExtractor(new DocumentProcess(), Options.Create(new AssistenteDocumentSettings())), Options.Create(new AssistenteDocumentSettings { Enabled = true }));
    private static Task<AssistenteDocumentoDto> Upload(AssistenteDocumentService docs, Guid conversation, string name, string text, Guid? upload = null) =>
        docs.UploadAsync(conversation, upload ?? Guid.NewGuid(), name, new MemoryStream(Encoding.UTF8.GetBytes(text)), default);
    private sealed class DocumentMemoryStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public async Task SaveAsync(string id, Stream content, CancellationToken ct) { using var copy = new MemoryStream(); await content.CopyToAsync(copy, ct); Files.Add(id, copy.ToArray()); }
        public Task<Stream> GetAsync(string id, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream(Files[id]));
        public Task DeleteAsync(string id, CancellationToken ct) { Files.Remove(id); return Task.CompletedTask; }
    }
}
