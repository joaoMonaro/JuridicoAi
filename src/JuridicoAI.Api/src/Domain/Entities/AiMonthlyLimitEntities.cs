namespace JuridicoAI.Api.Domain.Entities;

public sealed class AiOrganizationMonthlyLimit
{
    public Guid OrganizationId { get; init; }
    public decimal MonthlyLimitUsd { get; set; }
    public Guid UpdatedById { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiMonthlyAccount
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public decimal LimitUsd { get; set; }
    public decimal ConsumedUsd { get; set; }
    public decimal ReservedUsd { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiUsageReservation
{
    public Guid Id { get; init; }
    public Guid AccountId { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public Guid SolicitacaoId { get; init; }
    public int AttemptCount { get; init; }
    public Guid ClientRequestId { get; init; }
    public Guid CorrelationId { get; init; }
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public string? OperationType { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string PricingVersion { get; init; } = string.Empty;
    public decimal InputCostPerMillionUsd { get; init; }
    public decimal CachedInputCostPerMillionUsd { get; init; }
    public decimal CacheWriteCostPerMillionUsd { get; init; }
    public decimal OutputCostPerMillionUsd { get; init; }
    public int EstimatedInputTokens { get; init; }
    public int MaxOutputTokens { get; init; }
    public decimal ReservedUsd { get; init; }
    public decimal? SettledUsd { get; set; }
    public int? ActualInputTokens { get; set; }
    public int? ActualCachedInputTokens { get; set; }
    public int? ActualCacheWriteTokens { get; set; }
    public int? ActualOutputTokens { get; set; }
    public string Status { get; set; } = "Reserved";
    public string? ErrorCode { get; set; }
    public Guid? ReconciledById { get; set; }
    public string? ReconciliationReason { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SettledAt { get; set; }
}

public sealed class AiFinancialAuditLog
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid? ReservationId { get; init; }
    public Guid ActorUserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public decimal? PreviousLimitUsd { get; init; }
    public decimal? NewLimitUsd { get; init; }
    public decimal? SettledUsd { get; init; }
    public DateTime CreatedAt { get; init; }
}
