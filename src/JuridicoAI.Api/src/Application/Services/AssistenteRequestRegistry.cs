using System.Collections.Concurrent;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteRequestRegistry
{
    private readonly ConcurrentDictionary<(Guid OrganizationId, Guid ClientRequestId), byte> _running = new();

    public bool TryStart(Guid organizationId, Guid clientRequestId) =>
        _running.TryAdd((organizationId, clientRequestId), 0);

    public void Complete(Guid organizationId, Guid clientRequestId) =>
        _running.TryRemove((organizationId, clientRequestId), out _);
}
