using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Documents;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace JuridicoAI.Api.Tests;
public sealed partial class AssistenteWorkspacePostgresTests
{
    private sealed record PersonalIdentity(Guid UserId) : IPersonalHistoryContext;
    private static PersonalHistoryService Personal(Database db, JuridicoAiDbContext ctx, IFileStorage storage, Guid? userId = null) => new(ctx, new PersonalIdentity(userId ?? db.UserId), storage);
    [ReviewPostgresFact]
    public async Task Pessoal_LeConteudoPersistidoSemMembershipOuPecaAtualENaoExpoeInternos()
    {
        await using var db = await Database.Create(); var proposal = await db.Prepare(); await using var context = db.Context();
        var storage = new DocumentMemoryStorage(); var service = Personal(db, context, storage);
        var doc = await Upload(Documents(db, context, storage), proposal.ConversaId, "privado.txt", "Anexo histórico do titular.");
        await context.Pecas.ExecuteUpdateAsync(s => s.SetProperty(p => p.Titulo, "TITULO_ATUAL_SECRETO").SetProperty(p => p.Versao, 9).SetProperty(p => p.DataExclusao, DateTime.UtcNow));
        await context.Organizations.ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrganizationStatusEnum.Inactive));
        await context.Memberships.Where(m => m.UserId == db.UserId).ExecuteDeleteAsync();
        Assert.Empty(await context.Memberships.Where(m => m.UserId == db.UserId).ToArrayAsync());
        var page = await service.ListAsync(null, 30, default); Assert.Single(page.Conversas); Assert.DoesNotContain("SECRETO", page.Conversas[0].Titulo);
        Assert.Equal(proposal.ConversaId, (await service.GetAsync(proposal.ConversaId, default)).Id);
        var messages = await service.MessagesAsync(proposal.ConversaId, null, 50, default); Assert.Equal(2, messages.Count);
        var serialized = JsonSerializer.Serialize(messages, AssistentePatchCompiler.JsonOptions);
        Assert.DoesNotContain("documentoBase", serialized); Assert.DoesNotContain("solicitacao", serialized); Assert.DoesNotContain("geracaoId", serialized);
        Assert.Equal(2, (await service.ProposalAsync(proposal.Id, default)).Grupos.Count);
        Assert.Single(await service.AttachmentsAsync(proposal.ConversaId, default));
        var file = await service.DownloadAsync(proposal.ConversaId, doc.Id, default); using var reader = new StreamReader(file.Stream); Assert.Equal("Anexo histórico do titular.", await reader.ReadToEndAsync());
    }

    [ReviewPostgresFact]
    public async Task Pessoal_TitularidadeBloqueiaLeituraDownloadPropostaEExclusaoAlheios()
    {
        await using var db = await Database.Create(); var proposal = await db.Prepare(); await using var context = db.Context(); var storage = new DocumentMemoryStorage();
        var file = await Upload(Documents(db, context, storage), proposal.ConversaId, "sigilo.txt", "Somente o titular.");
        var otherId = Guid.NewGuid(); context.Add(new User { Id = otherId, Nome = "Outro usuário", Email = otherId + "@example.test", PasswordHash = "ficticio", DataCriacao = DateTime.UtcNow, DataAtualizacao = DateTime.UtcNow }); await context.SaveChangesAsync();
        var foreign = Personal(db, context, storage, otherId);
        Assert.Empty((await foreign.ListAsync(null, 30, default)).Conversas);
        foreach (var call in new Func<Task>[] {
            async () => { await foreign.GetAsync(proposal.ConversaId, default); }, async () => { await foreign.MessagesAsync(proposal.ConversaId, null, 50, default); },
            async () => { await foreign.ProposalAsync(proposal.Id, default); }, async () => { await foreign.DownloadAsync(proposal.ConversaId, file.Id, default); },
            async () => { await foreign.DeleteAsync(proposal.ConversaId, new(Guid.NewGuid(), true), default); } })
            Assert.Equal(404, (await Assert.ThrowsAsync<AssistenteIaException>(call)).StatusCode);
        Assert.Single(await context.AssistenteConversas.ToArrayAsync()); Assert.Empty(await context.Set<AssistenteExclusao>().ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Pessoal_ExclusaoExigeConfirmacaoEPreservaPecaCasoAnexosAlheiosELedger()
    {
        await using var db = await Database.Create(); var proposal = await db.Prepare(); await using var ctx = db.Context(); var storage = new DocumentMemoryStorage();
        await db.Service(ctx).AplicarAsync(proposal.Id, Approve(proposal, proposal.Grupos[0].Id), default);
        var before = (await ctx.Pecas.AsNoTracking().SingleAsync()).ConteudoJson.RootElement.GetRawText();
        var docs = Documents(db, ctx, storage); var own = await Upload(docs, proposal.ConversaId, "privado.txt", "Conteúdo privado para remover.");
        await using (var extract = db.Context()) await Processor(extract, storage).ProcessNextAsync(default);
        var piece = await ctx.Pecas.AsNoTracking().SingleAsync(); var sharedId = Guid.NewGuid(); storage.Files["case/compartilhado.txt"] = Encoding.UTF8.GetBytes("Prova compartilhada.");
        ctx.Add(new DocumentoCaso { Id = sharedId, CasoId = piece.CasoId, NomeOriginal = "compartilhado.txt", StorageId = "case/compartilhado.txt", TamanhoBytes = 20, MimeType = "text/plain", Tipo = "Outro", DataUpload = DateTime.UtcNow });
        var otherUser = Guid.NewGuid(); ctx.Add(new User { Id = otherUser, Nome = "Outro titular", Email = otherUser + "@example.test", PasswordHash = "ficticio", DataCriacao = DateTime.UtcNow, DataAtualizacao = DateTime.UtcNow }); await ctx.SaveChangesAsync();
        var other = await db.Service(ctx, otherUser).ObterOuCriarAsync(db.PecaId, default);
        await Upload(Documents(db, ctx, storage, otherUser), other.Id, "alheio.txt", "Arquivo do outro chat.");
        await docs.SynchronizeAsync(proposal.ConversaId, default);
        var response = await ctx.AssistenteMensagens.AsNoTracking().FirstAsync(m => m.Autor == "Assistente");
        ctx.Add(new AssistenteMemoria { Id = Guid.NewGuid(), OrganizationId = db.OrgId, ConversaId = proposal.ConversaId, RespostaId = response.Id, Versao = 1, AteSequencia = 2, PoliticaVersao = "teste", Registros = JsonDocument.Parse("[]"), CriadaEm = DateTime.UtcNow }); await ctx.SaveChangesAsync();
        var personal = Personal(db, ctx, storage); var request = new DeleteConversationRequest(Guid.NewGuid(), false);
        Assert.Equal("AI_DELETION_CONFIRMATION_REQUIRED", (await Assert.ThrowsAsync<AssistenteIaException>(() => personal.DeleteAsync(proposal.ConversaId, request, default))).Code);
        Assert.False(await new AssistenteDeletionProcessor(ctx, storage).ProcessNextAsync(default));
        var scratch = AssistenteDocumentScratch.Create(proposal.ConversaId); await File.WriteAllTextAsync(Path.Combine(scratch, "source.txt"), "Cópia temporária privada.");
        var accepted = await personal.DeleteAsync(proposal.ConversaId, request with { ConfirmarExclusao = true }, default);
        Assert.Equal(accepted.RequestId, (await personal.DeleteAsync(proposal.ConversaId, request with { ConfirmarExclusao = true }, default)).RequestId);
        Assert.Equal(404, (await Assert.ThrowsAsync<AssistenteIaException>(() => personal.GetAsync(proposal.ConversaId, default))).StatusCode);
        Assert.Equal("AI_CONVERSATION_DELETING", (await Assert.ThrowsAsync<AssistenteIaException>(() => db.Service(ctx).ObterOuCriarAsync(db.PecaId, default))).Code);
        Assert.Equal(2, await ctx.AssistenteMensagens.CountAsync());
        await using (var worker = db.Context()) Assert.True(await new AssistenteDeletionProcessor(worker, storage).ProcessNextAsync(default));
        await using var verify = db.Context(); var result = await Personal(db, verify, storage).DeletionAsync(request.RequestId, default);
        Assert.Equal("RemovidaDoAmbienteAtivo", result.Estado); Assert.False(result.EliminadaDasCopias); Assert.False(Directory.Exists(AssistenteDocumentScratch.Private(proposal.ConversaId)));
        Assert.Equal(result, await Personal(db, verify, storage).DeleteAsync(proposal.ConversaId, request with { ConfirmarExclusao = true }, default));
        Assert.Empty(await verify.AssistenteMensagens.ToArrayAsync()); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync()); Assert.Empty(await verify.AssistenteRecibos.ToArrayAsync()); Assert.Empty(await verify.Set<AssistenteMemoria>().ToArrayAsync());
        Assert.Single(await verify.AssistenteConversas.IgnoreQueryFilters().ToArrayAsync()); Assert.Equal(other.Id, (await verify.AssistenteConversas.SingleAsync()).Id);
        Assert.Single(await verify.DocumentosCaso.ToArrayAsync()); Assert.Equal(2, await verify.AssistenteDocumentos.CountAsync());
        Assert.Equal(2, storage.Files.Count); Assert.Contains("case/compartilhado.txt", storage.Files.Keys); Assert.DoesNotContain(await verify.AssistenteDocumentos.ToArrayAsync(), d => d.Id == own.Id);
        Assert.Equal(before, (await verify.Pecas.SingleAsync()).ConteudoJson.RootElement.GetRawText()); Assert.Equal(2, (await verify.Pecas.SingleAsync()).Versao);
        Assert.Equal("[]", (await verify.Set<AssistenteExclusao>().SingleAsync()).Arquivos.RootElement.GetRawText());
        Assert.Equal("AI_IDEMPOTENCY_MISMATCH", (await Assert.ThrowsAsync<AssistenteIaException>(() => Personal(db, verify, storage).DeleteAsync(other.Id, request with { ConfirmarExclusao = true }, default))).Code);
        var next = await db.Service(verify).ObterOuCriarAsync(db.PecaId, default); Assert.NotEqual(proposal.ConversaId, next.Id);
    }

    [ReviewPostgresFact]
    public async Task Pessoal_ExclusaoComGeracaoEmVooImpedeRecriacaoOuPublicacaoTardia()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new GenerationModel { BeforeGeneration = async () => { entered.TrySetResult(); await release.Task; } };
        var (conversation, _) = await QueueGeneration(db, storage, model, documents: true);
        await using var generation = db.Context(); var running = GenerationServices(db, generation, storage, model).Processor.ProcessNextAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var deleting = db.Context()) await Personal(db, deleting, storage).DeleteAsync(conversation, new(Guid.NewGuid(), true), default);
        await using (var worker = db.Context()) Assert.True(await new AssistenteDeletionProcessor(worker, storage).ProcessNextAsync(default));
        release.TrySetResult(); Assert.True(await running);
        await using var verify = db.Context(); Assert.Empty(await verify.AssistenteConversas.IgnoreQueryFilters().ToArrayAsync()); Assert.Empty(await verify.Set<AssistenteGeracao>().ToArrayAsync());
        Assert.Empty(await verify.Set<AssistenteGeracaoEtapa>().ToArrayAsync()); Assert.Empty(await verify.AssistenteMensagens.ToArrayAsync()); Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
        Assert.Single(await verify.AiUsageLedger.ToArrayAsync()); Assert.Equal(1, (await verify.Pecas.SingleAsync()).Versao); Assert.Empty(storage.Files);
    }

    [ReviewPostgresFact]
    public async Task Pessoal_ExpurgoRemoveGrafoCompletoComAnaliseMemoriaESecoesConfirmadas()
    {
        await using var db = await Database.Create(); var storage = new DocumentMemoryStorage(); var model = new GenerationModel { AnalyzeFirst = true };
        var (conversation, _) = await QueueGeneration(db, storage, model, documents: true); await DrainGeneration(db, storage, model);
        await using (var authorize = db.Context()) await Personal(db, authorize, storage).DeleteAsync(conversation, new(Guid.NewGuid(), true), default);
        await using (var worker = db.Context()) Assert.True(await new AssistenteDeletionProcessor(worker, storage).ProcessNextAsync(default));
        await using var verify = db.Context(); Assert.Equal("RemovidaDoAmbienteAtivo", (await verify.Set<AssistenteExclusao>().SingleAsync()).Estado);
        Assert.Empty(await verify.Set<AssistenteAnaliseDocumental>().ToArrayAsync()); Assert.Empty(await verify.Set<AssistenteAnaliseLote>().ToArrayAsync()); Assert.Empty(await verify.Set<AssistenteGeracaoEtapa>().ToArrayAsync());
        Assert.Empty(await verify.AssistenteConversas.IgnoreQueryFilters().ToArrayAsync()); Assert.Equal(model.Calls + model.Analysis.GenerationCalls, await verify.AiUsageLedger.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Pessoal_FalhaDeArmazenamentoPreservaProtocoloERetomaSemReexporConversa()
    {
        await using var db = await Database.Create(); await using var ctx = db.Context(); var inner = new DocumentMemoryStorage(); var conversation = await db.Service(ctx).ObterOuCriarAsync(db.PecaId, default);
        var docs = Documents(db, ctx, inner); await Upload(docs, conversation.Id, "um.txt", "Primeiro arquivo."); await Upload(docs, conversation.Id, "dois.txt", "Segundo arquivo.");
        var protocol = await Personal(db, ctx, inner).DeleteAsync(conversation.Id, new(Guid.NewGuid(), true), default); var storage = new PersonalFlakyStorage(inner);
        await using (var worker = db.Context()) Assert.True(await new AssistenteDeletionProcessor(worker, storage).ProcessNextAsync(default));
        Assert.Single(inner.Files); Assert.Equal(404, (await Assert.ThrowsAsync<AssistenteIaException>(() => Personal(db, ctx, inner).GetAsync(conversation.Id, default))).StatusCode);
        Assert.Equal("PendenteDeRetomada", (await Personal(db, ctx, inner).DeletionAsync(protocol.RequestId, default)).Estado);
        await ctx.Set<AssistenteExclusao>().ExecuteUpdateAsync(s => s.SetProperty(d => d.DisponivelEm, DateTime.UtcNow));
        await using (var restart = db.Context()) Assert.True(await new AssistenteDeletionProcessor(restart, storage).ProcessNextAsync(default));
        Assert.Empty(inner.Files); Assert.Equal(3, storage.Deletes); Assert.Equal("RemovidaDoAmbienteAtivo", (await Personal(db, ctx, inner).DeletionAsync(protocol.RequestId, default)).Estado);
    }
    private sealed class PersonalFlakyStorage(DocumentMemoryStorage inner) : IFileStorage
    {
        public int Deletes;
        public Task SaveAsync(string id, Stream stream, CancellationToken ct) => inner.SaveAsync(id, stream, ct);
        public Task<Stream> GetAsync(string id, CancellationToken ct) => inner.GetAsync(id, ct);
        public Task DeleteAsync(string id, CancellationToken ct) { if (++Deletes == 2) throw new IOException("Falha sintética sem dados jurídicos."); return inner.DeleteAsync(id, ct); }
    }

    [ReviewPostgresFact]
    public async Task Pessoal_PaginacaoEstavelEmpatadaENaoConsultaTitulosAtuais()
    {
        await using var db = await Database.Create(); await using var ctx = db.Context(); var source = await ctx.Pecas.AsNoTracking().SingleAsync(); var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            var piece = new Peca { Id = Guid.NewGuid(), OrganizationId = db.OrgId, CasoId = source.CasoId, CreatedById = db.UserId, UpdatedById = db.UserId, Titulo = "ATUAL", ConteudoJson = JsonDocument.Parse("{}"), PageConfig = JsonDocument.Parse("{}"), Styles = JsonDocument.Parse("{}"), DataCriacao = now, DataAtualizacao = now };
            ctx.Add(piece); ctx.Add(new AssistenteConversa { Id = Guid.NewGuid(), OrganizationId = db.OrgId, UserId = db.UserId, PecaId = piece.Id, Titulo = "Título histórico " + i, CriadaEm = now, AtualizadaEm = now });
        }
        await ctx.SaveChangesAsync(); var service = Personal(db, ctx, new DocumentMemoryStorage()); var ids = new List<Guid>(); string? cursor = null;
        do { var page = await service.ListAsync(cursor, 2, default); ids.AddRange(page.Conversas.Select(c => c.Id)); Assert.All(page.Conversas, c => Assert.StartsWith("Título histórico", c.Titulo)); cursor = page.ProximoCursor; } while (cursor is not null);
        Assert.Equal(5, ids.Distinct().Count()); Assert.Equal(5, ids.Count);
        Assert.Equal(400, (await Assert.ThrowsAsync<AssistenteIaException>(() => service.ListAsync("cursor-invalido", 30, default))).StatusCode);
    }

    [ReviewPostgresFact]
    public async Task Pessoal_LoginSemOrganizacaoRefreshRotacionaELogoutRevogaSessaoPropria()
    {
        await using var db = await Database.Create(); await using var ctx = db.Context(); var hasher = new Argon2idPasswordHasher(); var user = await ctx.Users.SingleAsync(u => u.Id == db.UserId); user.PasswordHash = await hasher.HashAsync("Personal-test-123!", default); await ctx.SaveChangesAsync();
        var config = Options.Create(new AuthSettings { JwtSecret = "personal-synthetic-test-signing-secret-123456789" }); var service = new PersonalHistoryAuthService(ctx, hasher, new JwtTokenService(config), config);
        await ctx.Memberships.Where(m => m.UserId == db.UserId).ExecuteDeleteAsync(); Assert.Empty(await ctx.Memberships.Where(m => m.UserId == db.UserId).ToArrayAsync()); var response = await service.LoginAsync(new(user.Email, "Personal-test-123!"), default);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken); Assert.Equal([PersonalHistoryAuthentication.Audience], token.Audiences); Assert.DoesNotContain(token.Claims, c => c.Type is "role" or "organizationId");
        Assert.Equal(900, response.ExpiresIn); Assert.InRange(response.RefreshTokenExpiresAt, DateTime.UtcNow.AddDays(6.99), DateTime.UtcNow.AddDays(7.01));
        var rotated = await service.RefreshAsync(response.RefreshToken, default); Assert.NotEqual(response.RefreshToken, rotated.RefreshToken);
        await Assert.ThrowsAsync<NaoAutorizadoException>(() => service.RefreshAsync(response.RefreshToken, default));
        await service.LogoutAsync(response.RefreshToken, default); Assert.NotNull((await ctx.Set<PersonalHistorySession>().AsNoTracking().SingleAsync()).RevogadaEm);
        await Assert.ThrowsAsync<NaoAutorizadoException>(() => service.RefreshAsync(rotated.RefreshToken, default));
        user.Status = UserStatusEnum.Inactive; await ctx.SaveChangesAsync(); await Assert.ThrowsAsync<NaoAutorizadoException>(() => service.LoginAsync(new(user.Email, "Personal-test-123!"), default));
    }
}
