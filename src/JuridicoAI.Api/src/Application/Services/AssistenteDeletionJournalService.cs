using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteDeletionJournalService(JuridicoAiDbContext db, IAssistenteDeletionJournal journal)
{
    public static AssistenteDeletionAuthorization Authorization(AssistenteExclusao d) => new(d.Id, d.UserId, d.ConversaId, d.AutorizadaEm);
    public async Task<AssistenteExclusao> ConfirmAsync(Guid requestId, CancellationToken ct)
    {
        var record = await db.Set<AssistenteExclusao>().AsNoTracking().SingleAsync(d => d.Id == requestId, ct);
        // Independent durable write before acknowledgement; no database transaction spans this I/O.
        await journal.AppendAsync(Authorization(record), ct);
        var protectedAt = DateTime.UtcNow;
        await db.Set<AssistenteExclusao>().Where(d => d.Id == requestId && d.JournaledAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.JournaledAt, protectedAt), ct);
        record.JournaledAt ??= protectedAt;
        return record;
    }
    public async Task<int> ExportAsync(bool includeAlreadyProtected, CancellationToken ct)
    {
        var ids = await db.Set<AssistenteExclusao>().AsNoTracking().Where(d => includeAlreadyProtected || d.JournaledAt == null)
            .OrderBy(d => d.AutorizadaEm).Select(d => d.Id).Take(includeAlreadyProtected ? 100001 : 50).ToArrayAsync(ct);
        if (ids.Length > 100000) throw new InvalidOperationException("Registro de exclusões excede o limite operacional.");
        foreach (var id in ids) await ConfirmAsync(id, ct);
        return ids.Length;
    }
    public static async Task BlockAsync(JuridicoAiDbContext db, Guid id, CancellationToken ct)
    {
        await db.AssistenteConversas.IgnoreQueryFilters().Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "EmExclusao").SetProperty(c => c.ExclusaoEpoch, c => c.ExclusaoEpoch + 1), ct);
        await db.Set<AssistenteGeracao>().Where(j => j.ConversaId == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, "Cancelada").SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.LeaseAte, (DateTime?)null), ct);
        await db.Set<AssistenteAnaliseDocumental>().Where(j => j.ConversaId == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, "Cancelada").SetProperty(j => j.LeaseToken, (Guid?)null).SetProperty(j => j.LeaseAte, (DateTime?)null), ct);
        await db.AssistenteDocumentos.Where(d => d.ConversaId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, "Cancelado").SetProperty(d => d.LeaseToken, (Guid?)null).SetProperty(d => d.LeaseAte, (DateTime?)null), ct);
        await db.AssistenteMensagens.Where(m => m.ConversaId == id && (m.Estado == "Gerando" || m.Estado == "AguardandoAnalise" || m.Estado == "AguardandoGeracao"))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Estado, "Cancelada").SetProperty(m => m.CodigoErro, "AI_CONVERSATION_DELETING"), ct);
    }
}
