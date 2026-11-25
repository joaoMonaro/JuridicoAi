using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;

namespace JuridicoAI.Api.Infrastructure.Observability;

public sealed class DataAccessAuditService(
    JuridicoAiDbContext db,
    IAuthContext auth,
    ICorrelationContext correlation,
    TimeProvider? timeProvider = null) : IDataAccessAuditService
{
    public async Task RecordAsync(string action, string resourceType, Guid resourceId, CancellationToken cancellationToken)
    {
        var correlationId = correlation.CorrelationId == Guid.Empty
            ? null
            : correlation.CorrelationId.ToString("D");

        db.DataAccessAuditLogs.Add(new DataAccessAuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = auth.OrganizationId,
            UserId = auth.UserId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            CorrelationId = correlationId,
            OccurredAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
