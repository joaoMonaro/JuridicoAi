using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Services;
namespace JuridicoAI.Api.Infrastructure.Recovery;

// Retries independent writes only in a verified live environment; legacy export is an explicit CLI operation.
public sealed class DeletionJournalWorker(IServiceScopeFactory scopes, IAssistenteDeletionJournal journal,
    ILogger<DeletionJournalWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!journal.Configured) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<AssistenteRecoveryGate>().IsOpenAsync(ct) &&
                    await scope.ServiceProvider.GetRequiredService<AssistenteDeletionJournalService>().ExportAsync(false, ct) > 0) continue;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("AI_DELETION_JOURNAL_PENDING; autorizações preservadas, confirmação protegida pendente."); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
