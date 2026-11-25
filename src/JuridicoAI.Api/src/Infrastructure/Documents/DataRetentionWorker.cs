using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.Documents;

public sealed record DataRetentionSummary(
    int SessionsRemoved,
    int PersonalSessionsRemoved,
    int PecasRemoved,
    int TemplatesRemoved,
    int AuditLogsRemoved);

public sealed class DataRetentionWorker(
    IServiceScopeFactory scopes,
    IOptions<DataRetentionSettings> options,
    ILogger<DataRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;

        var interval = TimeSpan.FromHours(Math.Clamp(settings.IntervalHours, 1, 168));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<DataRetentionProcessor>();
                var summary = await processor.RunOnceAsync(stoppingToken);
                if (summary.SessionsRemoved + summary.PersonalSessionsRemoved + summary.PecasRemoved + summary.TemplatesRemoved + summary.AuditLogsRemoved > 0)
                {
                    logger.LogInformation(
                        "Expurgo periódico LGPD concluído. Sessions={Sessions} PersonalSessions={PersonalSessions} Pecas={Pecas} Templates={Templates} AuditLogs={AuditLogs}",
                        summary.SessionsRemoved,
                        summary.PersonalSessionsRemoved,
                        summary.PecasRemoved,
                        summary.TemplatesRemoved,
                        summary.AuditLogsRemoved);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                logger.LogError("Falha operacional no expurgo periódico de retenção; nova execução ocorrerá no próximo ciclo.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

public sealed class DataRetentionProcessor(
    JuridicoAiDbContext db,
    IOptions<DataRetentionSettings> options,
    TimeProvider? timeProvider = null)
{
    public async Task<DataRetentionSummary> RunOnceAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var softDeleteCutoff = now.AddDays(-settings.SoftDeleteRetentionDays);
        var sessionCutoff = now.AddDays(-settings.ExpiredSessionRetentionDays);
        var auditCutoff = now.AddDays(-settings.AuditLogRetentionDays);

        var sessionsRemoved = await db.Sessions
            .Where(s => s.ExpiraEm < sessionCutoff || (s.RevogadaEm != null && s.RevogadaEm < sessionCutoff))
            .ExecuteDeleteAsync(ct);

        var personalSessionsRemoved = await db.Set<PersonalHistorySession>()
            .Where(s => s.ExpiraEm < sessionCutoff || (s.RevogadaEm != null && s.RevogadaEm < sessionCutoff))
            .ExecuteDeleteAsync(ct);

        // Expurga fisicamente apenas peças soft-deleted sem conversas vinculadas (respeita FK Restrict).
        var pecasRemoved = await db.Pecas
            .Where(p => p.DataExclusao != null && p.DataExclusao < softDeleteCutoff &&
                        !db.AssistenteConversas.IgnoreQueryFilters().Any(c => c.PecaId == p.Id))
            .ExecuteDeleteAsync(ct);

        // Expurga fisicamente apenas templates soft-deleted não referenciados por peças existentes.
        var templatesRemoved = await db.TemplatesPeca
            .Where(t => t.DataExclusao != null && t.DataExclusao < softDeleteCutoff &&
                        !db.Pecas.Any(p => p.TemplateId == t.Id))
            .ExecuteDeleteAsync(ct);

        var auditLogsRemoved = await db.DataAccessAuditLogs
            .Where(a => a.OccurredAt < auditCutoff)
            .ExecuteDeleteAsync(ct);

        return new DataRetentionSummary(
            sessionsRemoved,
            personalSessionsRemoved,
            pecasRemoved,
            templatesRemoved,
            auditLogsRemoved);
    }
}
