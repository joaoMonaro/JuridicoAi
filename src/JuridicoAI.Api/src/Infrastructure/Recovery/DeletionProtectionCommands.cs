using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace JuridicoAI.Api.Infrastructure.Recovery;

public static class DeletionProtectionCommands
{
    public static async Task<bool> ExecuteAsync(string[] args, IServiceProvider services, CancellationToken ct)
    {
        var command = args.FirstOrDefault(a => a is "initialize-deletion-journal" or "export-deletion-authorizations" or
            "inspect-deletion-journal" or "restore-assistant-deletions" or "verify-restored-deletions");
        if (command is null) return false;
        await using var scope = services.CreateAsyncScope();
        var journal = scope.ServiceProvider.GetRequiredService<IAssistenteDeletionJournal>();
        if (!journal.Configured) throw FileDeletionJournal.Unavailable();
        if (command == "inspect-deletion-journal")
        {
            var snapshot = await journal.ReadAsync(ct);
            Console.WriteLine($"Autorizações: {snapshot.Records.Count}; SHA256: {snapshot.Sha256}"); return true;
        }
        var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
        await db.Database.MigrateAsync(ct);
        if (command == "initialize-deletion-journal")
        {
            if (Environment.GetEnvironmentVariable("AI_CONFIRM_NEW_JOURNAL") != "true") throw new InvalidOperationException("Inicialização exige AI_CONFIRM_NEW_JOURNAL=true e um destino independente novo ou já conferido.");
            await journal.InitializeAsync(ct);
        }
        if (command is "initialize-deletion-journal" or "export-deletion-authorizations")
        {
            var count = await scope.ServiceProvider.GetRequiredService<AssistenteDeletionJournalService>().ExportAsync(true, ct);
            Console.WriteLine($"Autorizações conferidas/exportadas: {count}."); return true;
        }
        var recovery = scope.ServiceProvider.GetRequiredService<AssistenteDeletionRecovery>();
        if (command == "verify-restored-deletions")
        {
            if (!await recovery.IsSafeForTrafficAsync(ct)) throw new InvalidOperationException("Restauração ainda não verificada; mantenha o ambiente fechado.");
            Console.WriteLine("Exclusões protegidas conferidas no banco e nos arquivos ativos."); return true;
        }
        if (!scope.ServiceProvider.GetRequiredService<IOptions<DeletionProtectionSettings>>().Value.MaintenanceMode)
            throw new InvalidOperationException("A reaplicação exige DeletionProtection:MaintenanceMode=true.");
        var completed = await recovery.ReplayAsync(Guid.TryParse(Environment.GetEnvironmentVariable("AI_RESTORE_RUN_ID"), out var run) ? run : Guid.Empty,
            Environment.GetEnvironmentVariable("AI_RESTORE_JOURNAL_SHA256") ?? "",
            Environment.GetEnvironmentVariable("AI_CONFIRM_RESTORE_ISOLATED") == "true",
            Environment.GetEnvironmentVariable("AI_CONFIRM_WORKERS_DRAINED") == "true",
            Environment.GetEnvironmentVariable("AI_RESTORE_EVIDENCE") ?? "", ct);
        Console.WriteLine($"Autorizações reaplicadas/conferidas: {completed}. Reinicie a aplicação somente após a verificação operacional."); return true;
    }
}
