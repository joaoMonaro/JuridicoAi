using System.Diagnostics.Metrics;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.Observability;

public sealed record AssistenteMonitoringPoint(string Metric, double Value, string Dimension = "", string Category = "");
public sealed record AssistenteMonitoringSnapshot(DateTime CollectedAt, IReadOnlyList<AssistenteMonitoringPoint> Points);

/// <summary>Callbacks only read memory. Database I/O never runs on the exporter's thread.</summary>
public sealed class AssistenteMonitoringState : IDisposable
{
    private readonly Meter meter = new(AssistenteTelemetry.MeterName);
    private AssistenteMonitoringSnapshot? snapshot;
    private readonly Counter<long> failures;
    public static readonly string[] Metrics = ["ai_jobs", "ai_job_oldest_age_seconds", "ai_jobs_stalled",
        "ai_jobs_deadline_exceeded", "ai_leases_expired", "ai_deletions_pending", "ai_deletion_oldest_age_seconds",
        "ai_deletions_overdue", "ai_reservations", "ai_reservation_oldest_age_seconds", "ai_reservations_overdue",
        "ai_monthly_consumed_usd", "ai_monthly_reserved_usd", "ai_budget_organizations", "ai_interactions_24h"];

    public AssistenteMonitoringState(TimeProvider clock)
    {
        foreach (var metric in Metrics)
        {
            meter.CreateObservableGauge(metric, () => Observe(metric));
        }
        meter.CreateObservableGauge("ai_monitoring_snapshot_available", () => Snapshot is null ? 0 : 1);
        meter.CreateObservableGauge("ai_monitoring_last_success_timestamp_seconds", () =>
            Snapshot is { } value ? new DateTimeOffset(value.CollectedAt).ToUnixTimeMilliseconds() / 1000d : 0);
        meter.CreateObservableGauge("ai_monitoring_snapshot_age_seconds", () => Snapshot is { } value
            ? Math.Max(0, (clock.GetUtcNow().UtcDateTime - value.CollectedAt).TotalSeconds) : 0);
        failures = meter.CreateCounter<long>("ai_monitoring_collection_failures");
    }

    public AssistenteMonitoringSnapshot? Snapshot => Volatile.Read(ref snapshot);
    public void Publish(AssistenteMonitoringSnapshot value) => Volatile.Write(ref snapshot, value);
    public void Failed() => AssistenteTelemetry.Safe(() => failures.Add(1));
    private IEnumerable<Measurement<double>> Observe(string metric)
    {
        // No sample before a successful collection; failure retains last data and its timestamp.
        var current = Snapshot;
        if (current is null) yield break;
        foreach (var point in current.Points.Where(p => p.Metric == metric))
            yield return new(point.Value, new KeyValuePair<string, object?>("category", point.Category),
                new KeyValuePair<string, object?>("dimension", point.Dimension));
    }
    public void Dispose() => meter.Dispose();
}

public sealed class AssistenteMonitoringRow
{
    public string Metric { get; set; } = "";
    public double Value { get; set; }
    public string Dimension { get; set; } = "";
    public string Category { get; set; } = "";
}

public sealed class AssistenteMonitoringCollector(JuridicoAiDbContext db, IOptions<AssistenteMonitoringSettings> options, TimeProvider clock)
{
    public async Task<AssistenteMonitoringSnapshot> CollectAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var stalled = now.AddSeconds(-settings.StalledJobSeconds);
        var deletion = now.AddSeconds(-settings.PendingDeletionSeconds);
        var reservation = now.AddSeconds(-settings.OldReservationSeconds);
        var recent = now.AddDays(-1);
        db.Database.SetCommandTimeout(settings.QueryTimeoutSeconds);
        // One statement gives a consistent snapshot. No identifiers or document columns are selected.
        var rows = await db.Database.SqlQuery<AssistenteMonitoringRow>($"""
            WITH jobs AS (
              SELECT 'generation' AS kind, estado, criada_em AS created, COALESCE(ultimo_progresso_em, criada_em) AS progress,
                lease_ate, prazo_em FROM ai_generations
                WHERE estado IN ('NaFila','PreparandoContexto','AguardandoDocumentos','AguardandoAnalise','Executando','Redigindo','Verificando','Publicando')
              UNION ALL
              SELECT 'analysis', estado, criada_em, COALESCE(ultimo_progresso_em, criada_em), lease_ate, prazo_em
                FROM ai_document_analyses WHERE estado IN ('NaFila','Lendo','Consolidando','Pronta','PreparandoResposta')
              UNION ALL
              SELECT 'document', estado, criado_em, COALESCE(ultimo_progresso_em, criado_em), lease_ate, NULL::timestamptz
                FROM ai_documents WHERE estado IN ('Recebido','Processando')
            ), job_groups AS (
              SELECT kind, estado, COUNT(*)::double precision AS amount,
                GREATEST(0, EXTRACT(EPOCH FROM ({now} - MIN(created))))::double precision AS age,
                COUNT(*) FILTER (WHERE progress < {stalled})::double precision AS stalled,
                COUNT(*) FILTER (WHERE prazo_em < {now})::double precision AS overdue,
                COUNT(*) FILTER (WHERE lease_ate < {now})::double precision AS expired
              FROM jobs GROUP BY kind, estado
            ), deletions AS (
              SELECT COUNT(*)::double precision AS amount,
                COALESCE(GREATEST(0, EXTRACT(EPOCH FROM ({now} - MIN(autorizada_em)))),0)::double precision AS age,
                COUNT(*) FILTER (WHERE autorizada_em < {deletion})::double precision AS overdue,
                COUNT(*) FILTER (WHERE lease_ate < {now})::double precision AS expired
              FROM ai_deletion_requests WHERE estado <> 'RemovidaDoAmbienteAtivo'
            ), reservations AS (
              SELECT status, COUNT(*)::double precision AS amount,
                GREATEST(0, EXTRACT(EPOCH FROM ({now} - MIN(created_at))))::double precision AS age,
                COUNT(*) FILTER (WHERE created_at < {reservation})::double precision AS overdue
              FROM ai_usage_reservations WHERE status IN ('Reserved','Indeterminate') GROUP BY status
            ), accounts AS (
              SELECT limit_usd, consumed_usd, reserved_usd FROM ai_monthly_accounts
                WHERE period_year = {now.Year} AND period_month = {now.Month}
            ), interactions AS (
              SELECT CASE WHEN estado IN ('Concluida','Rejeitada') THEN 'succeeded'
                WHEN estado IN ('Falhou','Interrompida') THEN 'failed' WHEN estado = 'Cancelada' THEN 'cancelled'
                ELSE 'active' END AS outcome, COUNT(*)::double precision AS amount
              FROM ai_messages WHERE autor = 'Assistente' AND criada_em >= {recent} GROUP BY 1
            )
            SELECT 'ai_jobs' AS "Metric", amount AS "Value", kind AS "Category", estado AS "Dimension" FROM job_groups
            UNION ALL SELECT 'ai_job_oldest_age_seconds', age, kind, estado FROM job_groups
            UNION ALL SELECT 'ai_jobs_stalled', stalled, kind, estado FROM job_groups
            UNION ALL SELECT 'ai_jobs_deadline_exceeded', overdue, kind, estado FROM job_groups
            UNION ALL SELECT 'ai_leases_expired', expired, kind, estado FROM job_groups
            UNION ALL SELECT 'ai_leases_expired', expired, 'deletion', 'pending' FROM deletions
            UNION ALL SELECT 'ai_deletions_pending', amount, '', '' FROM deletions
            UNION ALL SELECT 'ai_deletion_oldest_age_seconds', age, '', '' FROM deletions
            UNION ALL SELECT 'ai_deletions_overdue', overdue, '', '' FROM deletions
            UNION ALL SELECT 'ai_reservations', amount, '', status FROM reservations
            UNION ALL SELECT 'ai_reservation_oldest_age_seconds', age, '', status FROM reservations
            UNION ALL SELECT 'ai_reservations_overdue', overdue, '', status FROM reservations
            UNION ALL SELECT 'ai_monthly_consumed_usd', COALESCE(SUM(consumed_usd),0)::double precision, '', '' FROM accounts
            UNION ALL SELECT 'ai_monthly_reserved_usd', COALESCE(SUM(reserved_usd),0)::double precision, '', '' FROM accounts
            UNION ALL SELECT 'ai_budget_organizations', COUNT(*) FILTER (WHERE consumed_usd + reserved_usd >= limit_usd * 0.7)::double precision, '', '70' FROM accounts
            UNION ALL SELECT 'ai_budget_organizations', COUNT(*) FILTER (WHERE consumed_usd + reserved_usd >= limit_usd * 0.9)::double precision, '', '90' FROM accounts
            UNION ALL SELECT 'ai_budget_organizations', COUNT(*) FILTER (WHERE consumed_usd + reserved_usd >= limit_usd)::double precision, '', '100' FROM accounts
            UNION ALL SELECT 'ai_interactions_24h', amount, '', outcome FROM interactions
            """).ToArrayAsync(ct);
        var points = rows.Select(r => new AssistenteMonitoringPoint(r.Metric, r.Value, r.Dimension, r.Category)).ToList();
        // Export explicit zeros for known missing groups to clear alerts when the last job finishes.
        var states = new Dictionary<string, string[]>
        {
            ["generation"] = ["NaFila", "PreparandoContexto", "AguardandoDocumentos", "AguardandoAnalise", "Executando", "Redigindo", "Verificando", "Publicando"],
            ["analysis"] = ["NaFila", "Lendo", "Consolidando", "Pronta", "PreparandoResposta"],
            ["document"] = ["Recebido", "Processando"]
        };
        foreach (var (kind, categories) in states)
        foreach (var category in categories)
        foreach (var metric in new[] { "ai_jobs", "ai_job_oldest_age_seconds", "ai_jobs_stalled", "ai_jobs_deadline_exceeded", "ai_leases_expired" })
            if (!points.Any(p => p.Metric == metric && p.Category == kind && p.Dimension == category)) points.Add(new(metric, 0, category, kind));
        foreach (var status in new[] { "Reserved", "Indeterminate" })
        foreach (var metric in new[] { "ai_reservations", "ai_reservation_oldest_age_seconds", "ai_reservations_overdue" })
            if (!points.Any(p => p.Metric == metric && p.Dimension == status)) points.Add(new(metric, 0, status));
        foreach (var outcome in new[] { "succeeded", "failed", "cancelled", "active" })
            if (!points.Any(p => p.Metric == "ai_interactions_24h" && p.Dimension == outcome)) points.Add(new("ai_interactions_24h", 0, outcome));
        return new(now, points);
    }
}

public sealed class AssistenteMonitoringWorker(IServiceScopeFactory scopes, AssistenteMonitoringState state,
    IOptions<AssistenteMonitoringSettings> options, TimeProvider clock, ILogger<AssistenteMonitoringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            await CollectOnceAsync(stoppingToken);
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.CollectionIntervalSeconds), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
    public async Task CollectOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var snapshot = await scope.ServiceProvider.GetRequiredService<AssistenteMonitoringCollector>().CollectAsync(ct);
            state.Publish(snapshot);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception)
        {
            state.Failed();
            logger.LogWarning(new EventId(6501, "AssistenteMonitoringCollectionFailed"),
                "AI_MONITORING_COLLECTION_FAILED; últimos dados preservados com sua data de coleta.");
        }
    }
}
