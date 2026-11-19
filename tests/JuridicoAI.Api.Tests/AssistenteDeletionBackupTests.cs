using System.Diagnostics;
using System.Text;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Documents;
using JuridicoAI.Api.Infrastructure.Recovery;
using Microsoft.EntityFrameworkCore;
namespace JuridicoAI.Api.Tests;

public sealed class ReviewBackupFactAttribute : FactAttribute
{
    public ReviewBackupFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JURIDICOAI_REVIEW_TEST_POSTGRES")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JURIDICOAI_REVIEW_POSTGRES_CONTAINER")))
            Skip="Defina o PostgreSQL descartável e seu container para ensaiar pg_dump/pg_restore.";
    }
}
public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewBackupFact]
    public async Task P05_BackupRealDeBancoEArquivosAnteriorAoConsentimentoNaoRessuscitaConteudoExcluido()
    {
        await using var d=await Database.Create(); using var f=new DeletionJournalFixture(); await f.Journal.InitializeAsync(default);
        var proposal=await d.Prepare(); string dbName,body; string[] ownPaths; Guid otherConversation;
        await using(var ctx=d.Context())
        {
            dbName=ctx.Database.GetDbConnection().Database; Assert.StartsWith("review_",dbName);
            await d.Service(ctx).AplicarAsync(proposal.Id,Approve(proposal,proposal.Grupos[0].Id),default);
            body=(await ctx.Pecas.AsNoTracking().SingleAsync()).ConteudoJson.RootElement.GetRawText();
            await Upload(Documents(d,ctx,f.Storage),proposal.ConversaId,"PRIVATE_UPLOAD_NAME.txt","PRIVATE_TEXT_TO_DELETE");
            ownPaths=(await f.Catalog.ListAsync(proposal.ConversaId,default)).ToArray();
            await f.Storage.SaveAsync("case/shared.txt",new MemoryStream(Encoding.UTF8.GetBytes("SHARED_CASE_EVIDENCE")),default);
            var piece=await ctx.Pecas.AsNoTracking().SingleAsync();
            ctx.Add(new DocumentoCaso { Id=Guid.NewGuid(),CasoId=piece.CasoId,NomeOriginal="shared.txt",StorageId="case/shared.txt",TamanhoBytes=20,MimeType="text/plain",Tipo="Outro",DataUpload=DateTime.UtcNow });
            var other=Guid.NewGuid(); ctx.Add(new User { Id=other,Nome="Outro titular",Email=$"{other:N}@example.test",PasswordHash="fixture",DataCriacao=DateTime.UtcNow,DataAtualizacao=DateTime.UtcNow }); await ctx.SaveChangesAsync();
            otherConversation=(await d.Service(ctx,other).ObterOuCriarAsync(d.PecaId,default)).Id;
            await Upload(Documents(d,ctx,f.Storage,other),otherConversation,"outro.txt","OTHER_PRIVATE_CHAT");
            ctx.Add(new Session { Id=Guid.NewGuid(),UserId=d.UserId,OrganizationId=d.OrgId,RefreshTokenHash="restore-session",DataCriacao=DateTime.UtcNow,DataAtualizacao=DateTime.UtcNow,ExpiraEm=DateTime.UtcNow.AddDays(1) });
            ctx.Add(new PersonalHistorySession { Id=Guid.NewGuid(),UserId=d.UserId,RefreshTokenHash="restore-personal",RevocationTokenHash="restore-personal-revoke",CriadaEm=DateTime.UtcNow,AtualizadaEm=DateTime.UtcNow,ExpiraEm=DateTime.UtcNow.AddDays(1) }); await ctx.SaveChangesAsync();
        }
        var dump=Path.Combine(f.Root,"database.dump"); var files=Path.Combine(f.Root,"file-backup");
        await BackupDocker(dbName,dump,restore:false); CopyFiles(f.Documents,files);
        var request=Guid.NewGuid();
        await using(var ctx=d.Context())
        {
            await new PersonalHistoryService(ctx,new PersonalIdentity(d.UserId),f.Storage,f.Journal).DeleteAsync(proposal.ConversaId,new(request,true),default);
            await new AssistenteDeletionProcessor(ctx,f.Storage,f.Journal).ProcessNextAsync(default);
        }
        Assert.Empty(await f.Catalog.ListAsync(proposal.ConversaId,default));
        var fingerprint=(await f.Journal.ReadAsync(default)).Sha256;
        var journalText=await File.ReadAllTextAsync(f.FilePath); Assert.DoesNotContain("PRIVATE_",journalText);
        await BackupDocker(dbName,dump,restore:true);
        Directory.Delete(f.Documents,true); CopyFiles(files,f.Documents);
        await using(var ctx=d.Context())
        {
            Assert.True(await ctx.AssistenteConversas.IgnoreQueryFilters().AnyAsync(c=>c.Id==proposal.ConversaId));
            var processor=new AssistenteDeletionProcessor(ctx,f.Storage,f.Journal);
            var recovery=new AssistenteDeletionRecovery(ctx,f.Journal,f.Catalog,f.Storage,processor);
            Assert.False(await recovery.IsSafeForTrafficAsync(default));
            var run=Guid.NewGuid(); Assert.Equal(1,await recovery.ReplayAsync(run,fingerprint,true,true,"Backup fictício restaurado sem tráfego/workers.",default));
            Assert.True(await recovery.IsSafeForTrafficAsync(default));
            Assert.False(await ctx.AssistenteConversas.IgnoreQueryFilters().AnyAsync(c=>c.Id==proposal.ConversaId));
            Assert.Empty(await f.Catalog.ListAsync(proposal.ConversaId,default));
            Assert.Single(await f.Catalog.ListAsync(otherConversation,default)); Assert.True(await ctx.AssistenteConversas.AnyAsync(c=>c.Id==otherConversation));
            Assert.Equal(body,(await ctx.Pecas.AsNoTracking().SingleAsync()).ConteudoJson.RootElement.GetRawText());
            Assert.Equal(2,(await ctx.Pecas.AsNoTracking().SingleAsync()).Versao); Assert.Single(await ctx.DocumentosCaso.ToArrayAsync());
            await using(var shared=await f.Storage.GetAsync("case/shared.txt",default)) { using var reader=new StreamReader(shared); Assert.Equal("SHARED_CASE_EVIDENCE",await reader.ReadToEndAsync()); }
            Assert.NotNull((await ctx.Set<Session>().AsNoTracking().SingleAsync()).RevogadaEm);
            Assert.NotNull((await ctx.Set<PersonalHistorySession>().AsNoTracking().SingleAsync()).RevogadaEm);
            Assert.Equal(request,(await ctx.Set<AssistenteExclusao>().AsNoTracking().SingleAsync()).Id);
            Assert.False(PersonalHistoryService.DeletionDto(await ctx.Set<AssistenteExclusao>().AsNoTracking().SingleAsync()).EliminadaDasCopias);
            // File-only rollback after protocol completion must also be detected and re-purged.
            foreach(var path in ownPaths)
            {
                var source=Path.Combine(files,path.Replace('/',Path.DirectorySeparatorChar)); await using var stream=File.OpenRead(source);
                await f.Storage.SaveAsync(path,stream,default);
            }
            Assert.False(await recovery.IsSafeForTrafficAsync(default));
            Assert.Equal(1,await recovery.ReplayAsync(run,fingerprint,true,true,"Somente arquivos privados restaurados novamente.",default));
            Assert.True(await recovery.IsSafeForTrafficAsync(default)); Assert.Empty(await f.Catalog.ListAsync(proposal.ConversaId,default));
        }
    }
    private static void CopyFiles(string source,string destination)
    {
        Directory.CreateDirectory(destination);
        foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
        { var target=Path.Combine(destination,Path.GetRelativePath(source,file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file,target); }
    }
    private static async Task BackupDocker(string database,string dump,bool restore)
    {
        if(!database.StartsWith("review_",StringComparison.Ordinal)) throw new InvalidOperationException("Somente bancos exclusivos da suíte.");
        var container=Environment.GetEnvironmentVariable("JURIDICOAI_REVIEW_POSTGRES_CONTAINER")!;
        var start=new ProcessStartInfo("docker") { RedirectStandardError=true,RedirectStandardOutput=true,RedirectStandardInput=restore,UseShellExecute=false };
        foreach(var argument in restore ? new[]{"exec","-i",container,"pg_restore","-U","postgres","--clean","--if-exists","--no-owner","--no-acl","--exit-on-error","-d",database} :
            new[]{"exec",container,"pg_dump","-U","postgres","-Fc","--no-owner","--no-acl",database}) start.ArgumentList.Add(argument);
        using var process=Process.Start(start)!; var error=process.StandardError.ReadToEndAsync();
        if(restore)
        { await using var input=File.OpenRead(dump); await input.CopyToAsync(process.StandardInput.BaseStream); process.StandardInput.Close(); await process.StandardOutput.ReadToEndAsync(); }
        else { await using var output=File.Create(dump); await process.StandardOutput.BaseStream.CopyToAsync(output); }
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60)); Assert.True(process.ExitCode==0,await error);
    }
}
