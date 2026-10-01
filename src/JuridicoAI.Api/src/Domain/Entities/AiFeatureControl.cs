using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class AiOperationalControl
{
    public AiOperationalFeatureEnum Feature { get; init; }
    public bool Enabled { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedById { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class AiFeatureChangeAudit
{
    public Guid Id { get; init; }
    public AiFeatureScopeEnum Scope { get; init; }
    public Guid? OrganizationId { get; init; }
    public string Feature { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public DateTime ChangedAt { get; init; }
    public Guid ChangedById { get; init; }
    public string Reason { get; init; } = string.Empty;
}
