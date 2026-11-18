namespace JuridicoAI.Api.Domain.Entities;

public sealed class AiUsageLedgerEntry
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public Guid PecaId { get; init; }
    public Guid SolicitacaoId { get; init; }
    public Guid ClientRequestId { get; init; }
    public Guid CorrelationId { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string? Operation { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int? InputTokens { get; init; }
    public int? CachedInputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public decimal? EstimatedCostUsd { get; init; }
    public string PricingVersion { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
    public long DurationMs { get; init; }
    public DateTime CreatedAt { get; init; }
}
