using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteUsageLimiter(IOptionsMonitor<AssistenteIaSettings> options) : IAssistenteUsageLimiter, IDisposable
{
    private readonly object _admissionLock = new();
    private readonly ConcurrentDictionary<Guid, FixedWindowRateLimiter> _userRates = new();
    private readonly ConcurrentDictionary<Guid, FixedWindowRateLimiter> _organizationRates = new();
    private readonly ConcurrentDictionary<Guid, ConcurrencyLimiter> _userConcurrency = new();
    private readonly ConcurrentDictionary<Guid, ConcurrencyLimiter> _organizationConcurrency = new();

    public ValueTask<IAsyncDisposable> AdquirirAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // All probes and acquisitions are nonblocking (QueueLimit=0). Keep this
        // local admission atomic: a denied attempt must not consume rate permits.
        lock (_admissionLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var settings = options.CurrentValue;
            var acquired = new List<RateLimitLease>(4);
            var userRate = _userRates.GetOrAdd(userId, _ => CreateRate(settings.RequestsPerUserPerMinute));
            var organizationRate = _organizationRates.GetOrAdd(organizationId, _ => CreateRate(settings.RequestsPerOrganizationPerMinute));
            try
            {
                using var userProbe = Acquire(userRate, 0, "AI_USER_RATE_LIMITED",
                    "O limite de solicitações de IA por usuário foi atingido.");
                using var organizationProbe = Acquire(organizationRate, 0, "AI_ORGANIZATION_RATE_LIMITED",
                    "O limite de solicitações de IA da organização foi atingido.");
                acquired.Add(Acquire(_userConcurrency.GetOrAdd(userId, _ => CreateConcurrency(settings.ConcurrentRequestsPerUser)),
                    1, "AI_USER_CONCURRENCY_LIMITED", "Já existem solicitações de IA demais em andamento para este usuário."));
                acquired.Add(Acquire(_organizationConcurrency.GetOrAdd(organizationId, _ => CreateConcurrency(settings.ConcurrentRequestsPerOrganization)),
                    1, "AI_ORGANIZATION_CONCURRENCY_LIMITED", "Já existem solicitações de IA demais em andamento nesta organização."));
                acquired.Add(Acquire(userRate, 1, "AI_USER_RATE_LIMITED",
                    "O limite de solicitações de IA por usuário foi atingido."));
                acquired.Add(Acquire(organizationRate, 1, "AI_ORGANIZATION_RATE_LIMITED",
                    "O limite de solicitações de IA da organização foi atingido."));
                return ValueTask.FromResult<IAsyncDisposable>(new CompositeLease(acquired));
            }
            catch
            {
                foreach (var lease in acquired) lease.Dispose();
                throw;
            }
        }
    }

    private static RateLimitLease Acquire(RateLimiter limiter, int permits, string code, string message)
    {
        var lease = limiter.AttemptAcquire(permits);
        if (!lease.IsAcquired)
        {
            var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : (TimeSpan?)null;
            lease.Dispose();
            throw new AssistenteIaException(StatusCodes.Status429TooManyRequests, code, message,
                retryable: true, retryAfter: retryAfter);
        }
        return lease;
    }

    private static FixedWindowRateLimiter CreateRate(int permitLimit) => new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true
    });

    private static ConcurrencyLimiter CreateConcurrency(int permitLimit) => new(new ConcurrencyLimiterOptions
    {
        PermitLimit = permitLimit,
        QueueLimit = 0
    });

    public void Dispose()
    {
        foreach (var limiter in _userRates.Values) limiter.Dispose();
        foreach (var limiter in _organizationRates.Values) limiter.Dispose();
        foreach (var limiter in _userConcurrency.Values) limiter.Dispose();
        foreach (var limiter in _organizationConcurrency.Values) limiter.Dispose();
    }

    private sealed class CompositeLease(IReadOnlyList<RateLimitLease> leases) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            for (var index = leases.Count - 1; index >= 0; index--) leases[index].Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
