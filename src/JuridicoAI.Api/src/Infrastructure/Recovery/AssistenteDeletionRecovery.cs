using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Recovery;

public sealed class AssistenteDeletionRecovery(JuridicoAiDbContext db, IAssistenteDeletionJournal journal,
    IPrivateConversationFiles catalog, IFileStorage storage, AssistenteDeletionProcessor processor)
{
    private static InvalidOperationException Unsafe() => new("Recuperação de exclusões não verificada; mantenha o ambiente isolado e confira o registro independente.");
    private async Task<IReadOnlyList<string>> FilesAsync(AssistenteDeletionAuthorization record, CancellationToken ct)
    {
        var conversation = await db.AssistenteConversas.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c => c.Id == record.ConversationId, ct);
        if (conversation is not null && conversation.UserId != record.UserId) throw Unsafe();
        var protocols = await db.Set<AssistenteExclusao>().AsNoTracking().Where(d => d.Id == record.RequestId || d.ConversaId == record.ConversationId).ToArrayAsync(ct);
        if (protocols.Any(d => AssistenteDeletionJournalService.Authorization(d) != record)) throw Unsafe();
        var documents = await db.AssistenteDocumentos.AsNoTracking().Where(d => d.ConversaId == record.ConversationId).ToArrayAsync(ct);
        if (documents.Any(d => d.DocumentoCasoId != null)) throw Unsafe();
        var files = (await catalog.ListAsync(record.ConversationId, ct)).Concat(documents.Select(d => d.StorageId))
            .Concat(protocols.SelectMany(d => AssistenteChatService.Read<string[]>(d.Arquivos) ?? throw Unsafe())).Distinct().ToArray();
        if (files.Any(f => !PrivateConversationFileCatalog.InScope(record.ConversationId, f)) ||
            await db.DocumentosCaso.AnyAsync(d => files.Contains(d.StorageId), ct) ||
            await db.AssistenteDocumentos.AnyAsync(d => files.Contains(d.StorageId) && (d.ConversaId != record.ConversationId || d.DocumentoCasoId != null), ct)) throw Unsafe();
        FileDeletionJournal.EnsureNoLinks(AssistenteDocumentScratch.Private(record.ConversationId));
        return files;
    }
    public async Task<bool> IsSafeForTrafficAsync(CancellationToken ct)
    {
        if (await db.Set<AssistenteRestoreState>().AnyAsync(s => s.InProgress, ct)) return false;
        var snapshot = await journal.ReadAsync(ct);
        var protocols = await db.Set<AssistenteExclusao>().AsNoTracking().ToArrayAsync(ct);
        // Missing external authorizations cannot be repaired by trusting a restored database.
        if (protocols.Any(d => d.JournaledAt == null || !snapshot.Records.Contains(AssistenteDeletionJournalService.Authorization(d)))) return false;
        foreach (var record in snapshot.Records)
        {
            var files = await FilesAsync(record, ct);
            var conversation = await db.AssistenteConversas.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c => c.Id == record.ConversationId, ct);
            var protocol = protocols.SingleOrDefault(d => d.Id == record.RequestId);
            if (conversation is not null && (conversation.Estado != "EmExclusao" || protocol is null || protocol.Estado == "RemovidaDoAmbienteAtivo")) return false;
            var actual = await catalog.ListAsync(record.ConversationId, ct);
            if (protocol is null || protocol.Estado == "RemovidaDoAmbienteAtivo")
            {
                if (actual.Count != 0 || Directory.Exists(AssistenteDocumentScratch.Private(record.ConversationId))) return false;
            }
            else if (actual.Except(AssistenteChatService.Read<string[]>(protocol.Arquivos)!).Any()) return false;
        }
        return true;
    }
    public async Task<int> ReplayAsync(Guid runId, string expectedSha256, bool isolated, bool workersDrained, string evidence, CancellationToken ct)
    {
        if (runId == Guid.Empty || !isolated || !workersDrained || string.IsNullOrWhiteSpace(evidence) ||
            expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit)) throw Unsafe();
        var snapshot = await journal.ReadAsync(ct);
        if (!string.Equals(snapshot.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase)) throw Unsafe();
        // Session advisory lock spans I/O without keeping a database transaction open.
        await db.Database.OpenConnectionAsync(ct);
        var locked = false;
        try
        {
            locked = await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(50520261119) AS \"Value\"").SingleAsync(ct);
            if (!locked) throw Unsafe();
            var state = await db.Set<AssistenteRestoreState>().SingleOrDefaultAsync(ct);
            if (state is null) { state = new(); db.Add(state); }
            state.InProgress = true; state.RunId = runId; state.JournalSha256 = snapshot.Sha256; state.StartedAt = DateTime.UtcNow; state.VerifiedAt = null;
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            // Revoke both login surfaces restored from backups, before any deletion can fail.
            await db.Set<Session>().Where(s => s.RevogadaEm == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevogadaEm, DateTime.UtcNow), ct);
            await db.Set<PersonalHistorySession>().Where(s => s.RevogadaEm == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevogadaEm, DateTime.UtcNow), ct);
            var existing = await db.Set<AssistenteExclusao>().AsNoTracking().ToArrayAsync(ct);
            if (existing.Any(d => !snapshot.Records.Contains(AssistenteDeletionJournalService.Authorization(d)))) throw Unsafe();
            // Preflight all ownership/storage bindings before deleting any file.
            var plans = new List<(AssistenteDeletionAuthorization Record, IReadOnlyList<string> Files)>();
            foreach (var record in snapshot.Records) plans.Add((record, await FilesAsync(record, ct)));
            foreach (var (record, files) in plans)
            {
                if (!await db.Users.AnyAsync(u => u.Id == record.UserId, ct))
                {
                    // The backup may predate the user. Only the signed private namespace can remain.
                    foreach (var file in files) await storage.DeleteAsync(file, ct);
                    AssistenteDocumentScratch.DeletePrivate(record.ConversationId);
                    continue;
                }
                await using (var tx = await db.Database.BeginTransactionAsync(ct))
                {
                    await db.AssistenteConversas.FromSqlInterpolated($"SELECT * FROM ai_conversations WHERE id = {record.ConversationId} FOR UPDATE")
                        .IgnoreQueryFilters().AsNoTracking().ToArrayAsync(ct);
                    await AssistenteDeletionJournalService.BlockAsync(db, record.ConversationId, ct);
                    var request = await db.Set<AssistenteExclusao>().SingleOrDefaultAsync(d => d.Id == record.RequestId, ct);
                    if (request is null)
                    {
                        request = new() { Id = record.RequestId, UserId = record.UserId, ConversaId = record.ConversationId, AutorizadaEm = record.AuthorizedAt };
                        db.Add(request);
                    }
                    request.Estado = "Autorizada"; request.Arquivos = AssistentePatchCompiler.Json(files); request.JournaledAt = DateTime.UtcNow;
                    request.DisponivelEm = DateTime.UtcNow; request.ConcluidaEm = null; request.LeaseToken = null; request.LeaseAte = null; request.CodigoErro = null;
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
                }
                if (!await processor.ProcessNextAsync(ct, record.RequestId)) throw Unsafe();
                if (!await db.Set<AssistenteExclusao>().AnyAsync(d => d.Id == record.RequestId && d.Estado == "RemovidaDoAmbienteAtivo", ct)) throw Unsafe();
            }
            foreach (var record in snapshot.Records)
                if (await db.AssistenteConversas.IgnoreQueryFilters().AnyAsync(c => c.Id == record.ConversationId, ct) ||
                    await db.AssistenteDocumentos.AnyAsync(d => d.ConversaId == record.ConversationId, ct) ||
                    (await catalog.ListAsync(record.ConversationId, ct)).Count != 0 || Directory.Exists(AssistenteDocumentScratch.Private(record.ConversationId))) throw Unsafe();
            if ((await journal.ReadAsync(ct)).Sha256 != snapshot.Sha256) throw Unsafe();
            await db.Set<AssistenteRestoreState>().Where(s => s.Id == 1 && s.RunId == runId).ExecuteUpdateAsync(s =>
                s.SetProperty(s => s.InProgress, false).SetProperty(s => s.VerifiedAt, DateTime.UtcNow), ct);
            return snapshot.Records.Count;
        }
        finally
        {
            if (locked) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(50520261119)", CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }
}
