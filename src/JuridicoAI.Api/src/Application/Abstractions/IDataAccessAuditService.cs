namespace JuridicoAI.Api.Application.Abstractions;

public interface IDataAccessAuditService
{
    Task RecordAsync(string action, string resourceType, Guid resourceId, CancellationToken cancellationToken);
}
