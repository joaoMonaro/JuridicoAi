using System.Diagnostics;
using System.Diagnostics.Metrics;
using JuridicoAI.Api.Application.Abstractions;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteTelemetry : IAssistenteTelemetry
{
    public const string MeterName = "JuridicoAI.Assistente";
    public static AssistenteTelemetry Shared { get; } = new();
    private readonly Meter meter = new(MeterName, "1.0.0");
    private readonly Histogram<double> stages;
    private readonly Histogram<double> calls;
    private readonly Counter<long> tokens;
    private readonly Counter<long> unknownUsage;
    private readonly Counter<long> workerFailures;
    private readonly Counter<long> leaseLost;
    private readonly Counter<long> errors;
    private static readonly HashSet<string> Stages = ["chat.accept", "generation.context", "generation.prepare",
        "generation.processing", "generation.publish", "provider.input_tokens", "provider.headers", "provider.generation"];

    private AssistenteTelemetry()
    {
        stages = meter.CreateHistogram<double>("ai.stage.duration", "s");
        calls = meter.CreateHistogram<double>("ai.provider.call.duration", "s");
        tokens = meter.CreateCounter<long>("ai.provider.tokens", "{token}");
        unknownUsage = meter.CreateCounter<long>("ai.provider.usage.unknown", "{call}");
        workerFailures = meter.CreateCounter<long>("ai.worker.failures", "{failure}");
        errors = meter.CreateCounter<long>("ai.operational.events", "{event}");
        leaseLost = meter.CreateCounter<long>("ai.worker.lease.lost", "{lease}");
    }

    // A listener/exporter must never change the outcome of a business operation.
    public void Stage(string stage, double milliseconds) => Safe(() => stages.Record(
        Math.Max(0, milliseconds) / 1000, new KeyValuePair<string, object?>("stage", Stages.Contains(stage) ? stage : "other")));
    public void ProviderCall(AssistenteProviderOperation operation, AssistenteCallOutcome outcome, double milliseconds) =>
        Safe(() => calls.Record(Math.Max(0, milliseconds) / 1000,
            new("operation", operation == AssistenteProviderOperation.Generation ? "generation" : "input_tokens"), new("outcome", outcome.ToString().ToLowerInvariant())));
    public void ProviderTokens(int? input, int? cachedInput, int? output) => Safe(() =>
    {
        if (input is null || output is null) unknownUsage.Add(1);
        // Cached input is a subset of input, not an additional billed token count.
        if (input is { } i) tokens.Add(Math.Max(0, i), new KeyValuePair<string, object?>("kind", "input"));
        if (cachedInput is { } c) tokens.Add(Math.Clamp(c, 0, input ?? c), new KeyValuePair<string, object?>("kind", "cached_input"));
        if (output is { } o) tokens.Add(Math.Max(0, o), new KeyValuePair<string, object?>("kind", "output"));
    });
    public void WorkerFailure(AssistenteWorkerKind worker) => Safe(() => workerFailures.Add(1,
        new KeyValuePair<string, object?>("worker", worker.ToString().ToLowerInvariant())));
    public void WorkerLeaseLost(AssistenteWorkerKind worker) => Safe(() => leaseLost.Add(1,
        new KeyValuePair<string, object?>("worker", worker.ToString().ToLowerInvariant())));
    public void OperationalEvent(string code) => Safe(() => errors.Add(1, new KeyValuePair<string, object?>("reason", code switch
    {
        "AI_USER_RATE_LIMITED" or "AI_ORGANIZATION_RATE_LIMITED" => "rate_limit",
        "AI_USER_CONCURRENCY_LIMITED" or "AI_ORGANIZATION_CONCURRENCY_LIMITED" => "concurrency",
        "AI_MONTHLY_LIMIT_REACHED" or "AI_MONTHLY_LIMIT_INSUFFICIENT" => "budget",
        "AI_PROVIDER_TIMEOUT" or "AI_GENERATION_DEADLINE" or "AI_ANALYSIS_DEADLINE" => "deadline",
        "AI_GENERATION_LEASE_LOST" or "AI_ANALYSIS_LEASE_LOST" => "lease_lost",
        "AI_LIMIT_CONFIGURATION_INVALID" or "AI_CONFIGURATION_DIVERGED" => "configuration",
        "AI_DOCUMENTS_PROCESSING" => "documents_processing",
        _ => "other"
    })));
    public static void Safe(Action action) { try { action(); } catch (Exception) { /* Best effort telemetry only. */ } }

    public static ProviderMeasurement MeasureProvider(AssistenteProviderOperation operation) => new(operation);
    public sealed class ProviderMeasurement(AssistenteProviderOperation operation) : IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private bool sent;
        private AssistenteCallOutcome outcome = AssistenteCallOutcome.Failed;
        public bool WasDispatched => sent;
        public void Dispatched() => sent = true;
        public void Succeeded() => outcome = AssistenteCallOutcome.Succeeded;
        public void Cancelled() => outcome = AssistenteCallOutcome.Cancelled;
        public void Dispose()
        {
            if (sent) Shared.ProviderCall(operation, outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
