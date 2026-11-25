namespace JuridicoAI.Api.Domain.Entities;

/// <summary>
/// Registro de auditoria de acesso/download/exportação de dados pessoais (LGPD Art. 37 e Art. 46/48).
/// Armazena apenas identificadores e metadados operacionais — nunca dados pessoais em claro.
/// </summary>
public sealed class DataAccessAuditLog
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public Guid ResourceId { get; init; }
    public string? CorrelationId { get; init; }
    public DateTime OccurredAt { get; init; }
}
