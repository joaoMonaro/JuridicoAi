using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;

namespace JuridicoAI.Api.Application.Services;

public static class AssistenteFinancialMeter
{
    public static void Attach(
        AssistenteDocumentTools tools,
        IAssistenteMonthlyLimitService monthlyLimit,
        IAssistenteUsageRecorder usageRecorder,
        Guid organizationId,
        Guid userId,
        Guid pecaId,
        Guid solicitacaoId,
        Guid clientRequestId,
        Guid correlationId,
        AssistenteModo modo,
        string provider,
        string expectedModel,
        string promptVersion,
        string operationType,
        bool requireDurablePersistence = false)
    {
        AssistenteFinancialReservation? pending = null;
        var ordinal = 0;

        tools.BeforeFinancialGeneration = async (inputTokens, maxOutputTokens, ct) =>
        {
            ordinal++;
            var reservation = await monthlyLimit.ReservarAsync(
                new AssistenteFinancialReserveRequest(
                    organizationId,
                    userId,
                    solicitacaoId,
                    ordinal,
                    clientRequestId,
                    correlationId,
                    operationType,
                    inputTokens,
                    maxOutputTokens),
                ct);
            if (!reservation.IsBypassedSimulation &&
                !string.Equals(reservation.Model, expectedModel, StringComparison.Ordinal))
            {
                await monthlyLimit.LiberarAntesDoEnvioAsync(reservation, "AI_CONFIGURATION_DIVERGED", CancellationToken.None);
                throw new Exceptions.AssistenteIaException(
                    StatusCodes.Status503ServiceUnavailable,
                    "AI_CONFIGURATION_DIVERGED",
                    "O assistente de IA está temporariamente indisponível.");
            }
            pending = reservation;
        };

        tools.ReleasePendingFinancial = async (errorCode, ct) =>
        {
            var current = pending;
            pending = null;
            if (current is not null)
                await monthlyLimit.LiberarAntesDoEnvioAsync(current, errorCode, ct);
        };

        tools.SettleFinancialCall = async call =>
        {
            var current = pending;
            pending = null;
            if (current is null) return null;
            return await monthlyLimit.FinalizarChamadaAsync(current, call, CancellationToken.None);
        };

        tools.RecordCallWithOutcome = (call, outcome) =>
            usageRecorder.RegistrarAsync(
                new AssistenteUsageRecord(
                    organizationId,
                    userId,
                    pecaId,
                    solicitacaoId,
                    clientRequestId,
                    correlationId,
                    modo,
                    null,
                    provider,
                    call.Model,
                    promptVersion,
                    call.Ordinal,
                    call.InputTokens,
                    call.CachedInputTokens,
                    call.OutputTokens,
                    call.Status,
                    call.ErrorCode,
                    call.DurationMs,
                    RequirePersistence: requireDurablePersistence || monthlyLimit.ControleFinanceiroAtivo,
                    FrozenEstimatedCostUsd: outcome?.SettledUsd,
                    FrozenPricingVersion: outcome?.PricingVersion,
                    CacheWriteTokens: call.CacheWriteTokens),
                CancellationToken.None);
    }
}
