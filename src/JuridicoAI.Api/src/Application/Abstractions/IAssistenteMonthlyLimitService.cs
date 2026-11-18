using JuridicoAI.Api.Application.DTOs;

namespace JuridicoAI.Api.Application.Abstractions;

public interface IAssistenteMonthlyLimitService
{
    bool ControleFinanceiroAtivo { get; }

    Task<AssistenteLimiteMensalDto> ObterResumoAsync(Guid organizationId, CancellationToken cancellationToken);

    Task ValidarPreCondicaoSolicitacaoAsync(Guid organizationId, CancellationToken cancellationToken);

    Task ValidarDisponibilidadeAsync(
        Guid organizationId,
        int estimatedInputTokens,
        int maxOutputTokens,
        CancellationToken cancellationToken);

    Task<AssistenteFinancialReservation> ReservarAsync(
        AssistenteFinancialReserveRequest request,
        CancellationToken cancellationToken);

    Task<AssistenteCallSettlementOutcome> LiquidarAsync(
        AssistenteFinancialReservation reservation,
        int inputTokens,
        int? cachedInputTokens,
        int outputTokens,
        string? errorCode,
        CancellationToken cancellationToken);

    Task LiberarAntesDoEnvioAsync(
        AssistenteFinancialReservation reservation,
        string errorCode,
        CancellationToken cancellationToken);

    Task MarcarIndeterminadaAsync(
        AssistenteFinancialReservation reservation,
        string errorCode,
        CancellationToken cancellationToken);

    Task<AssistenteCallSettlementOutcome> FinalizarChamadaAsync(
        AssistenteFinancialReservation reservation,
        AssistenteProviderCall call,
        CancellationToken cancellationToken);

    Task DefinirLimiteOrganizacaoAsync(
        Guid organizationId,
        decimal monthlyLimitUsd,
        Guid updatedById,
        string reason,
        CancellationToken cancellationToken);

    Task<AssistenteReconciliationResult> ReconciliarIndeterminadaAsync(
        Guid reservationId,
        bool confirmarConsumo,
        int? inputTokens,
        int? cachedInputTokens,
        int? outputTokens,
        Guid operatorUserId,
        string reason,
        CancellationToken cancellationToken);

    Task<AssistenteReconciliationResult> RecuperarReservaOrfaAsync(
        Guid reservationId,
        AssistenteOrphanRecoveryAction action,
        bool confirmWorkersDrained,
        string drainEvidence,
        int? inputTokens,
        int? cachedInputTokens,
        int? outputTokens,
        Guid operatorUserId,
        string reason,
        CancellationToken cancellationToken);
}

public enum AssistenteOrphanRecoveryAction
{
    MarkIndeterminate,
    Settle,
    Release
}

public sealed record AssistenteFinancialReserveRequest(
    Guid OrganizationId,
    Guid UserId,
    Guid SolicitacaoId,
    int AttemptCount,
    Guid ClientRequestId,
    Guid CorrelationId,
    string OperationType,
    int EstimatedInputTokens,
    int MaxOutputTokens);

public sealed record AssistenteFinancialReservation(
    Guid ReservationId,
    Guid AccountId,
    Guid OrganizationId,
    int PeriodYear,
    int PeriodMonth,
    string Provider,
    string Model,
    string PricingVersion,
    decimal InputCostPerMillionUsd,
    decimal CachedInputCostPerMillionUsd,
    decimal OutputCostPerMillionUsd,
    decimal ReservedUsd,
    bool IsBypassedSimulation = false);

public sealed record AssistenteCallSettlementOutcome(
    string Status,
    decimal? SettledUsd,
    string PricingVersion);

public sealed record AssistenteReconciliationResult(
    Guid ReservationId,
    Guid OrganizationId,
    int PeriodYear,
    int PeriodMonth,
    string Status,
    decimal? SettledUsd,
    decimal ReservedUsd);

public sealed class AssistenteProviderTurnTracker
{
    public bool PreSendMarked { get; private set; }
    public bool Dispatched { get; private set; }
    public bool SafeToRelease { get; private set; }
    public bool ProvenBeforeSend => PreSendMarked && !Dispatched;
    public string? Model { get; private set; }
    public int? InputTokens { get; private set; }
    public int? CachedInputTokens { get; private set; }
    public int? OutputTokens { get; private set; }

    public bool HasKnownUsage => InputTokens.HasValue && OutputTokens.HasValue;

    public void MarkPreSend()
    {
        PreSendMarked = true;
        Dispatched = false;
        SafeToRelease = false;
    }

    public void MarkDispatched()
    {
        Dispatched = true;
    }

    public void MarkRejectedBeforeExecution()
    {
        SafeToRelease = true;
    }

    public void ObserveUsage(string? model, int? inputTokens, int? cachedInputTokens, int? outputTokens)
    {
        if (!string.IsNullOrWhiteSpace(model)) Model = model;
        if (inputTokens.HasValue) InputTokens = inputTokens.Value;
        if (cachedInputTokens.HasValue) CachedInputTokens = cachedInputTokens.Value;
        if (outputTokens.HasValue) OutputTokens = outputTokens.Value;
    }
}
