using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
namespace JuridicoAI.Api.Infrastructure.Documents;

// Always runs for accepted authorizations, independently of AI rollout flags or Membership.
public sealed class AssistenteDeletionWorker(IServiceScopeFactory scopes, ILogger<AssistenteDeletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await using var scope = scopes.CreateAsyncScope(); if (await scope.ServiceProvider.GetRequiredService<AssistenteDeletionProcessor>().ProcessNextAsync(ct)) continue; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Falha operacional no expurgo autorizado do assistente; a fila será retomada."); }
            try { await Task.Delay(1500, ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
public sealed class AssistenteDeletionProcessor(JuridicoAiDbContext db, IFileStorage storage)
{
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        AssistenteExclusao? job; var now = DateTime.UtcNow; var lease = Guid.NewGuid();
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            job = await db.Set<AssistenteExclusao>().FromSqlInterpolated($"""
                SELECT * FROM ai_deletion_requests WHERE estado <> 'RemovidaDoAmbienteAtivo' AND disponivel_em <= {now}
                AND (lease_ate IS NULL OR lease_ate < {now}) ORDER BY autorizada_em LIMIT 1 FOR UPDATE SKIP LOCKED
                """).AsNoTracking().SingleOrDefaultAsync(ct);
            if (job is null) { await tx.CommitAsync(ct); return false; }
            await db.Set<AssistenteExclusao>().Where(d => d.Id == job.Id).ExecuteUpdateAsync(s => s.SetProperty(d => d.LeaseToken, lease).SetProperty(d => d.LeaseAte, now.AddSeconds(120))
                .SetProperty(d => d.Tentativas, d => d.Tentativas + 1).SetProperty(d => d.Estado, "RemovendoArquivos"), ct);
            await tx.CommitAsync(ct);
        }
        try
        {
            var remaining = AssistenteChatService.Read<string[]>(job.Arquivos)!;
            while (remaining.Length > 0)
            {
                if (!await OwnAsync(job.Id, lease, ct)) return true;
                await storage.DeleteAsync(remaining[0], ct);
                remaining = remaining.Skip(1).ToArray();
                if (await db.Set<AssistenteExclusao>().Where(d => d.Id == job.Id && d.LeaseToken == lease && d.LeaseAte > DateTime.UtcNow).ExecuteUpdateAsync(s =>
                    s.SetProperty(d => d.Arquivos, AssistentePatchCompiler.Json(remaining)).SetProperty(d => d.LeaseAte, DateTime.UtcNow.AddSeconds(120)), ct) != 1) return true;
            }
            if (!await OwnAsync(job.Id, lease, ct)) return true;
            AssistenteDocumentScratch.DeletePrivate(job.ConversaId);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Same content lock order as application: conversation before dependent rows. No storage/provider call holds this transaction.
            var conversation = await db.AssistenteConversas.FromSqlInterpolated($"SELECT * FROM ai_conversations WHERE id = {job.ConversaId} FOR UPDATE").IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(ct);
            if (!await OwnAsync(job.Id, lease, ct)) return true;
            if (conversation is not null)
            {
                if (conversation.UserId != job.UserId || conversation.Estado != "EmExclusao") throw new InvalidOperationException("Escopo de expurgo inválido.");
                var id = job.ConversaId;
                await db.Set<AssistenteMemoria>().Where(m => m.ConversaId == id).ExecuteDeleteAsync(ct);
                await db.AssistenteRecibos.Where(r => r.ConversaId == id).ExecuteDeleteAsync(ct);
                await db.AssistenteMensagens.Where(m => m.ConversaId == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.GeracaoId, (Guid?)null).SetProperty(m => m.AnaliseDocumentalId, (Guid?)null)
                    .SetProperty(m => m.PropostaId, (Guid?)null).SetProperty(m => m.RespostaAId, (Guid?)null), ct);
                await db.Set<AssistenteGeracaoEtapa>().Where(s => db.Set<AssistenteGeracao>().Any(j => j.Id == s.GeracaoId && j.ConversaId == id)).ExecuteDeleteAsync(ct);
                await db.Set<AssistenteGeracao>().Where(j => j.ConversaId == id).ExecuteDeleteAsync(ct);
                await db.Set<AssistenteAnaliseLote>().Where(b => db.Set<AssistenteAnaliseDocumental>().Any(j => j.Id == b.AnaliseId && j.ConversaId == id)).ExecuteDeleteAsync(ct);
                await db.Set<AssistenteAnaliseDocumental>().Where(j => j.ConversaId == id).ExecuteDeleteAsync(ct);
                await db.AssistenteMensagens.Where(m => m.ConversaId == id).ExecuteDeleteAsync(ct);
                await db.AssistentePropostas.Where(p => p.ConversaId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.SubstituidaPorId, (Guid?)null), ct);
                await db.AssistentePropostas.Where(p => p.ConversaId == id).ExecuteDeleteAsync(ct);
                await db.AssistenteDocumentoTrechos.Where(c => db.AssistenteDocumentos.Any(d => d.Id == c.DocumentoId && d.ConversaId == id)).ExecuteDeleteAsync(ct);
                await db.AssistenteDocumentos.Where(d => d.ConversaId == id).ExecuteDeleteAsync(ct);
                await db.AssistenteConversas.IgnoreQueryFilters().Where(c => c.Id == id && c.Estado == "EmExclusao").ExecuteDeleteAsync(ct);
            }
            var changed = await db.Set<AssistenteExclusao>().Where(d => d.Id == job.Id && d.LeaseToken == lease && d.LeaseAte > DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, "RemovidaDoAmbienteAtivo").SetProperty(d => d.ConcluidaEm, DateTime.UtcNow)
                    .SetProperty(d => d.Arquivos, AssistentePatchCompiler.Json(Array.Empty<string>())).SetProperty(d => d.CodigoErro, (string?)null)
                    .SetProperty(d => d.LeaseToken, (Guid?)null).SetProperty(d => d.LeaseAte, (DateTime?)null), ct);
            if (changed != 1) throw new InvalidOperationException("Lease de exclusão substituído.");
            await tx.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            db.ChangeTracker.Clear();
            await db.Set<AssistenteExclusao>().Where(d => d.Id == job.Id && d.LeaseToken == lease)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, "PendenteDeRetomada").SetProperty(d => d.CodigoErro, "AI_DELETION_RETRY_PENDING")
                    .SetProperty(d => d.LeaseToken, (Guid?)null).SetProperty(d => d.LeaseAte, (DateTime?)null).SetProperty(d => d.DisponivelEm, DateTime.UtcNow.AddSeconds(15)), CancellationToken.None);
        }
        return true;
    }
    private Task<bool> OwnAsync(Guid id, Guid lease, CancellationToken ct) => db.Set<AssistenteExclusao>().AnyAsync(d => d.Id == id && d.LeaseToken == lease && d.LeaseAte > DateTime.UtcNow && d.Estado != "RemovidaDoAmbienteAtivo", ct);
}
