using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class OrganizationFeature
{
    public Guid OrganizationId { get; init; }
    public OrganizationFeatureEnum Feature { get; init; }
    public bool Enabled { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}
