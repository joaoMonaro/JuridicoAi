using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteUsageRecorder(
    IAiUsageLedgerRepository repository,
    IOptionsMonitor<AssistenteIaSettings> options,
    ILogger<AssistenteUsageRecorder> logger) : IAssistenteUsageRecorder
{
    public async Task RegistrarAsync(AssistenteUsageRecord record, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var estimatedCost = CalculateCost(record, settings);
        var entry = new AiUsageLedgerEntry
        {
            Id = Guid.NewGuid(),
            OrganizationId = record.OrganizationId,
            UserId = record.UserId,
            PecaId = record.PecaId,
            SolicitacaoId = record.SolicitacaoId,
            ClientRequestId = record.ClientRequestId,
            CorrelationId = record.CorrelationId,
            Mode = record.Modo.ToString(),
            Operation = record.Operacao?.ToString(),
            Provider = record.Provider,
            Model = record.Model,
            PromptVersion = record.PromptVersion,
            AttemptCount = record.AttemptCount,
            InputTokens = record.InputTokens,
            CachedInputTokens = record.CachedInputTokens,
            OutputTokens = record.OutputTokens,
            EstimatedCostUsd = estimatedCost,
            PricingVersion = settings.PricingVersion,
            Status = record.Status,
            ErrorCode = record.ErrorCode,
            DurationMs = record.DurationMs,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await repository.AdicionarAsync(entry, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !record.RequirePersistence)
        {
            logger.LogError(
                "Falha ao registrar consumo de IA. SolicitacaoId={SolicitacaoId} CorrelationId={CorrelationId} ErrorType={ErrorType}",
                record.SolicitacaoId,
                record.CorrelationId,
                ex.GetType().Name);
        }
    }

    private static decimal? CalculateCost(AssistenteUsageRecord record, AssistenteIaSettings settings)
    {
        if (record.InputTokens is null && record.OutputTokens is null) return null;
        if (settings.InputCostPerMillionTokensUsd == 0 &&
            settings.CachedInputCostPerMillionTokensUsd == 0 &&
            settings.OutputCostPerMillionTokensUsd == 0)
            return null;
        var cached = Math.Min(record.CachedInputTokens ?? 0, record.InputTokens ?? 0);
        var uncached = Math.Max(0, (record.InputTokens ?? 0) - cached);
        return (uncached * settings.InputCostPerMillionTokensUsd
                + cached * settings.CachedInputCostPerMillionTokensUsd
                + (record.OutputTokens ?? 0) * settings.OutputCostPerMillionTokensUsd) / 1_000_000m;
    }
}
