using Microsoft.Extensions.DependencyInjection;
using System.Text;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Documents;
using JuridicoAI.Api.Infrastructure.Recovery;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    private sealed class MemoryPrivateCatalog(DocumentMemoryStorage storage) : IPrivateConversationFiles
    {
        public Task<IReadOnlyList<string>> ListAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(
            storage.Files.Keys.Where(p => PrivateConversationFileCatalog.InScope(id, p)).Order().ToArray());
    }
    private static AssistenteDeletionRecovery Recovery(Database d, JuridicoAiDbContext ctx, DocumentMemoryStorage storage) =>
        new(ctx, d.DeletionJournal, new MemoryPrivateCatalog(storage), storage, new(ctx, storage, d.DeletionJournal));
    private static AssistenteDeletionAuthorization Record(Database d, Guid conversation) => DeletionJournalTests.Authorization() with { UserId = d.UserId, ConversationId = conversation };
    private static Task<int> Replay(AssistenteDeletionRecovery recovery, Guid? run = null) => recovery.ReplayAsync(run ?? Guid.NewGuid(), new string('a',64), true, true, "Ensaio fictício isolado.", default);

    [ReviewPostgresFact]
    public async Task P05_FalhaExternaBloqueiaConversaSemConfirmarNemExpurgarERetomaPeloRegistroPendente()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context(); var storage = new DocumentMemoryStorage();
        var conversation = await d.Service(ctx).ObterOuCriarAsync(d.PecaId, default);
        await Upload(Documents(d,ctx,storage),conversation.Id,"privado.txt","Conteúdo privado.");
        d.DeletionJournal.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => Personal(d,ctx,storage).DeleteAsync(conversation.Id,new(Guid.NewGuid(),true),default));
        var pending = await ctx.Set<AssistenteExclusao>().AsNoTracking().SingleAsync(); Assert.Null(pending.JournaledAt);
        var status = await Personal(d,ctx,storage).DeletionAsync(pending.Id,default);
        Assert.Equal("PendenteDeRetomada",status.Estado); Assert.Equal("AI_DELETION_JOURNAL_PENDING",status.CodigoErro);
        Assert.Equal("EmExclusao",(await ctx.AssistenteConversas.IgnoreQueryFilters().AsNoTracking().SingleAsync()).Estado);
        Assert.False(await new AssistenteDeletionProcessor(ctx,storage,d.DeletionJournal).ProcessNextAsync(default)); Assert.Single(storage.Files);
        d.DeletionJournal.Fail = false;
        Assert.Equal(1,await new AssistenteDeletionJournalService(ctx,d.DeletionJournal).ExportAsync(false,default));
        Assert.Single(d.DeletionJournal.Records);
        Assert.True(await new AssistenteDeletionProcessor(ctx,storage,d.DeletionJournal).ProcessNextAsync(default));
        Assert.Empty(storage.Files); Assert.Equal("RemovidaDoAmbienteAtivo",(await ctx.Set<AssistenteExclusao>().AsNoTracking().SingleAsync()).Estado);
    }
    [ReviewPostgresFact]
    public async Task P05_ReaplicaConsentimentoPosteriorAoBackupPreservaPecaCasoERevogaSessoes()
    {
        await using var d = await Database.Create(); var proposal = await d.Prepare(); var storage = new DocumentMemoryStorage();
        await using var ctx = d.Context();
        await d.Service(ctx).AplicarAsync(proposal.Id,Approve(proposal,proposal.Grupos[0].Id),default);
        var piece = await ctx.Pecas.AsNoTracking().SingleAsync(); var content = piece.ConteudoJson.RootElement.GetRawText();
        await Upload(Documents(d,ctx,storage),proposal.ConversaId,"privado.txt","Texto privado que não pode ressurgir.");
        storage.Files["case/shared.txt"] = Encoding.UTF8.GetBytes("Prova compartilhada.");
        ctx.Add(new DocumentoCaso { Id=Guid.NewGuid(),CasoId=piece.CasoId,NomeOriginal="shared.txt",StorageId="case/shared.txt",MimeType="text/plain",Tipo="Outro",TamanhoBytes=20,DataUpload=DateTime.UtcNow });
        ctx.Add(new Session { Id=Guid.NewGuid(),UserId=d.UserId,OrganizationId=d.OrgId,RefreshTokenHash="fixture-session",DataCriacao=DateTime.UtcNow,DataAtualizacao=DateTime.UtcNow,ExpiraEm=DateTime.UtcNow.AddDays(1) });
        ctx.Add(new PersonalHistorySession { Id=Guid.NewGuid(),UserId=d.UserId,RevocationTokenHash="fixture-revocation",RefreshTokenHash="fixture-personal",CriadaEm=DateTime.UtcNow,AtualizadaEm=DateTime.UtcNow,ExpiraEm=DateTime.UtcNow.AddDays(1) });
        await ctx.SaveChangesAsync();
        var record=Record(d,proposal.ConversaId); await d.DeletionJournal.AppendAsync(record,default);
        var service=Recovery(d,ctx,storage); Assert.False(await service.IsSafeForTrafficAsync(default));
        var run=Guid.NewGuid(); Assert.Equal(1,await Replay(service,run)); Assert.True(await service.IsSafeForTrafficAsync(default));
        Assert.Equal(1,await Replay(service,run)); Assert.Single(await ctx.Set<AssistenteExclusao>().ToArrayAsync());
        Assert.Equal(record.RequestId,(await ctx.Set<AssistenteExclusao>().AsNoTracking().SingleAsync()).Id);
        Assert.Empty(await ctx.AssistenteConversas.IgnoreQueryFilters().ToArrayAsync()); Assert.Empty(await ctx.AssistenteMensagens.ToArrayAsync());
        Assert.Equal(content,(await ctx.Pecas.AsNoTracking().SingleAsync()).ConteudoJson.RootElement.GetRawText());
        Assert.Equal(piece.Versao,(await ctx.Pecas.AsNoTracking().SingleAsync()).Versao);
        Assert.Single(await ctx.DocumentosCaso.ToArrayAsync()); Assert.Contains("case/shared.txt",storage.Files.Keys);
        Assert.NotNull((await ctx.Set<Session>().AsNoTracking().SingleAsync()).RevogadaEm);
        Assert.NotNull((await ctx.Set<PersonalHistorySession>().AsNoTracking().SingleAsync()).RevogadaEm);
        Assert.False((await ctx.Set<AssistenteRestoreState>().AsNoTracking().SingleAsync()).InProgress);
    }
    [ReviewPostgresFact]
    public async Task P05_ProtocoloConcluidoNaoDispensaExcluirArquivoPrivadoRestauradoSemMetadados()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context(); var storage = new DocumentMemoryStorage();
        var conversation=await d.Service(ctx).ObterOuCriarAsync(d.PecaId,default);
        await Upload(Documents(d,ctx,storage),conversation.Id,"privado.txt","Fictício."); var saved=storage.Files.Single();
        await Personal(d,ctx,storage).DeleteAsync(conversation.Id,new(Guid.NewGuid(),true),default);
        await new AssistenteDeletionProcessor(ctx,storage,d.DeletionJournal).ProcessNextAsync(default);
        storage.Files[saved.Key]=saved.Value; // Only the file backup is older than the completed protocol.
        var recovery=Recovery(d,ctx,storage); Assert.False(await recovery.IsSafeForTrafficAsync(default));
        await Replay(recovery); Assert.Empty(storage.Files); Assert.True(await recovery.IsSafeForTrafficAsync(default));
    }
    [ReviewPostgresFact]
    public async Task P05_BackupAnteriorAoUsuarioRemoveApenasNamespaceAssinadoSemInventarUsuarioOuProtocolo()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context(); var storage = new DocumentMemoryStorage();
        var record=DeletionJournalTests.Authorization(); var path=$"assistant/{Guid.NewGuid():N}/{record.ConversationId:N}/orphan.txt";
        storage.Files[path]=[1]; storage.Files["case/keep.txt"]=[2]; await d.DeletionJournal.AppendAsync(record,default);
        await Replay(Recovery(d,ctx,storage)); Assert.Single(storage.Files); Assert.Contains("case/keep.txt",storage.Files.Keys);
        Assert.False(await ctx.Users.AnyAsync(u=>u.Id==record.UserId)); Assert.Empty(await ctx.Set<AssistenteExclusao>().ToArrayAsync());
    }
    [ReviewPostgresFact]
    public async Task P05_RecusaTitularDivergenteMantemManutencaoESemExcluirArquivos()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context(); var storage = new DocumentMemoryStorage();
        var conversation=await d.Service(ctx).ObterOuCriarAsync(d.PecaId,default); await Upload(Documents(d,ctx,storage),conversation.Id,"privado.txt","Fictício.");
        await d.DeletionJournal.AppendAsync(Record(d,conversation.Id) with { UserId=Guid.NewGuid() },default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Replay(Recovery(d,ctx,storage)));
        Assert.Single(storage.Files); Assert.True((await ctx.Set<AssistenteRestoreState>().AsNoTracking().SingleAsync()).InProgress);
    }
    [ReviewPostgresFact]
    public async Task P05_RecusaArquivoPrivadoQueTambemFoiReferenciadoComoFonteCompartilhada()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context(); var storage = new DocumentMemoryStorage();
        var conversation=await d.Service(ctx).ObterOuCriarAsync(d.PecaId,default); await Upload(Documents(d,ctx,storage),conversation.Id,"privado.txt","Fictício.");
        var piece=await ctx.Pecas.SingleAsync();
        ctx.Add(new DocumentoCaso { Id=Guid.NewGuid(),CasoId=piece.CasoId,NomeOriginal="shared.txt",StorageId=storage.Files.Keys.Single(),MimeType="text/plain",Tipo="Outro",TamanhoBytes=1,DataUpload=DateTime.UtcNow }); await ctx.SaveChangesAsync();
        await d.DeletionJournal.AppendAsync(Record(d,conversation.Id),default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Replay(Recovery(d,ctx,storage)));
        Assert.Single(storage.Files); Assert.Single(await ctx.DocumentosCaso.ToArrayAsync());
    }
    [ReviewPostgresFact]
    public async Task P05_ExigeHashConfirmacoesERecusaRegistroExternoMaisAntigoQueProtocoloRestaurado()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context(); var storage = new DocumentMemoryStorage();
        var service=Recovery(d,ctx,storage);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReplayAsync(Guid.NewGuid(),new string('b',64),true,true,"isolado",default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReplayAsync(Guid.NewGuid(),new string('a',64),false,true,"isolado",default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReplayAsync(Guid.NewGuid(),new string('a',64),true,false,"isolado",default));
        Assert.Empty(await ctx.Set<AssistenteRestoreState>().ToArrayAsync());
        var conversation=await d.Service(ctx).ObterOuCriarAsync(d.PecaId,default); await Personal(d,ctx,storage).DeleteAsync(conversation.Id,new(Guid.NewGuid(),true),default);
        d.DeletionJournal.Records.Clear(); Assert.False(await service.IsSafeForTrafficAsync(default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Replay(service)); Assert.True((await ctx.Set<AssistenteRestoreState>().AsNoTracking().SingleAsync()).InProgress);
    }
    [ReviewPostgresFact]
    public async Task P05_FencePersistenteBloqueiaHttpEWorkersMesmoComInicializacaoAnteriorVerificada()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context();
        var status=new AssistenteRecoveryStatus { VerifiedAtStartup=true };
        var gate=new AssistenteRecoveryGate(ctx,status,Options.Create(new DeletionProtectionSettings()));
        Assert.True(await gate.IsOpenAsync(default));
        ctx.Add(new AssistenteRestoreState { RunId=Guid.NewGuid(),InProgress=true,StartedAt=DateTime.UtcNow }); await ctx.SaveChangesAsync();
        Assert.False(await gate.IsOpenAsync(default));
        var nextCalled=false; var middleware=new AssistenteRecoveryMiddleware(_=>{ nextCalled=true; return Task.CompletedTask; });
        var http=new DefaultHttpContext(); http.Request.Path="/auth/login"; http.Response.Body=new MemoryStream();
        await middleware.InvokeAsync(http,gate); Assert.Equal(503,http.Response.StatusCode); Assert.False(nextCalled);
        http=new DefaultHttpContext(); http.Request.Path="/health/live"; await middleware.InvokeAsync(http,gate); Assert.True(nextCalled);
    }
    [ReviewPostgresFact]
    public async Task P05_FalhaNoExpurgoMantemFenceERetomadaNaoDuplicaAutorizacao()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context(); var storage = new DocumentMemoryStorage();
        var conversation=await d.Service(ctx).ObterOuCriarAsync(d.PecaId,default);
        await Upload(Documents(d,ctx,storage),conversation.Id,"um.txt","Primeiro.");
        await Upload(Documents(d,ctx,storage),conversation.Id,"dois.txt","Segundo.");
        await d.DeletionJournal.AppendAsync(Record(d,conversation.Id),default);
        var flaky=new PersonalFlakyStorage(storage);
        var service=new AssistenteDeletionRecovery(ctx,d.DeletionJournal,new MemoryPrivateCatalog(storage),flaky,new(ctx,flaky,d.DeletionJournal));
        var run=Guid.NewGuid(); await Assert.ThrowsAsync<InvalidOperationException>(()=>Replay(service,run));
        Assert.True((await ctx.Set<AssistenteRestoreState>().AsNoTracking().SingleAsync()).InProgress);
        Assert.Single(storage.Files); Assert.False(await service.IsSafeForTrafficAsync(default));
        Assert.Equal(1,await Replay(service,run)); Assert.Empty(storage.Files); Assert.True(await service.IsSafeForTrafficAsync(default));
        Assert.Single(await ctx.Set<AssistenteExclusao>().ToArrayAsync()); Assert.Single(d.DeletionJournal.Records);
    }
    [ReviewPostgresFact]
    public async Task P05_InicializacaoVerificaRegistroAntesDeAbrirEExigeReinicioAposRecuperacao()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context(); using var f=new DeletionJournalFixture();
        await f.Journal.InitializeAsync(default); var conversation=await d.Service(ctx).ObterOuCriarAsync(d.PecaId,default);
        await f.Journal.AppendAsync(Record(d,conversation.Id),default);
        var recovery=new AssistenteDeletionRecovery(ctx,f.Journal,f.Catalog,f.Storage,new(ctx,f.Storage,f.Journal));
        await using var services=new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddScoped<AssistenteDeletionRecovery>(_=>recovery).BuildServiceProvider();
        var status=new AssistenteRecoveryStatus();
        var startup=new AssistenteRecoveryStartup(services.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),status,f.Journal,
            Options.Create(f.Settings),Microsoft.Extensions.Logging.Abstractions.NullLogger<AssistenteRecoveryStartup>.Instance);
        await startup.StartAsync(default); Assert.False(status.VerifiedAtStartup);
        await recovery.ReplayAsync(Guid.NewGuid(),(await f.Journal.ReadAsync(default)).Sha256,true,true,"Isolado.",default);
        Assert.False(status.VerifiedAtStartup); await startup.StartAsync(default); Assert.True(status.VerifiedAtStartup);
        await File.WriteAllTextAsync(f.FilePath,"{}"); await startup.StartAsync(default); Assert.False(status.VerifiedAtStartup);
    }
    [ReviewPostgresFact]
    public async Task P05_ExportaTambemAutorizacaoConcluidaAnteriorAoUpgrade()
    {
        await using var d = await Database.Create(); await using var ctx = d.Context(); var storage=new DocumentMemoryStorage();
        var conversation=await d.Service(ctx).ObterOuCriarAsync(d.PecaId,default);
        await Personal(d,ctx,storage).DeleteAsync(conversation.Id,new(Guid.NewGuid(),true),default);
        await new AssistenteDeletionProcessor(ctx,storage,d.DeletionJournal).ProcessNextAsync(default);
        d.DeletionJournal.Records.Clear(); await ctx.Set<AssistenteExclusao>().ExecuteUpdateAsync(s=>s.SetProperty(d=>d.JournaledAt,(DateTime?)null));
        Assert.Equal(1,await new AssistenteDeletionJournalService(ctx,d.DeletionJournal).ExportAsync(true,default));
        Assert.Single(d.DeletionJournal.Records); Assert.NotNull((await ctx.Set<AssistenteExclusao>().AsNoTracking().SingleAsync()).JournaledAt);
    }
}
