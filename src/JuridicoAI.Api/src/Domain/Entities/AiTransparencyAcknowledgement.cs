namespace JuridicoAI.Api.Domain.Entities;

public sealed class AiTransparencyAcknowledgement
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public string NoticeVersion { get; init; } = string.Empty;
    public DateTime AcknowledgedAt { get; init; }
}
