using System.Diagnostics;

namespace JuridicoAI.Api.Application.Services;

/// <summary>Technical stage timings only. Never accepts prompt, result or exception content.</summary>
public static class AssistenteLatency
{
    public const int StageEventId = 6401;

    public static IDisposable Measure(ILogger? logger, string stage) => new Measurement(logger, stage);

    public static async Task<T> MeasureAsync<T>(ILogger? logger, string stage, Func<Task<T>> action)
    {
        using var measurement = Measure(logger, stage);
        return await action();
    }

    private sealed class Measurement(ILogger? logger, string stage) : IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            logger?.LogInformation(new EventId(StageEventId, "AssistenteLatency"),
                "AI_LATENCY Stage={Stage} DurationMs={DurationMs}", stage,
                Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 3));
        }
    }
}
