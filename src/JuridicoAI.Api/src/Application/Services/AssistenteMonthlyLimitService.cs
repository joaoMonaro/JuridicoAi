using System.Globalization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteMonthlyLimitService(
    JuridicoAiDbContext db,
    IOptionsMonitor<AssistenteIaSettings> options,
    IHostEnvironment environment,
    ILogger<AssistenteMonthlyLimitService> logger,
    TimeProvider? timeProvider = null) : IAssistenteMonthlyLimitService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public bool ControleFinanceiroAtivo => !IsSimulatedOptOut(options.CurrentValue);

    public async Task<AssistenteLimiteMensalDto> ObterResumoAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var now = _time.GetUtcNow().UtcDateTime;
        var period = $"{now.Year:D4}-{now.Month:D2}";

        if (IsSimulatedOptOut(settings))
            return new(period, AssistenteLimiteMensalEstado.Disponivel, 0m, 0m, 100m, false, false, null);

        if (!IsPricingValid(settings))
            return new(period, AssistenteLimiteMensalEstado.Indisponivel, null, null, null, false, true,
                "O assistente de IA está temporariamente indisponível.");

        var limitUsd = await ResolveEffectiveLimitUsdAsync(organizationId, settings, cancellationToken);
        var account = await db.AiMonthlyAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.PeriodYear == now.Year && x.PeriodMonth == now.Month, cancellationToken);

        var consumedUsd = account?.ConsumedUsd ?? 0m;
        var reservedUsd = account?.ReservedUsd ?? 0m;
        var hasReservations = reservedUsd > 0m;

        if (limitUsd <= 0m || consumedUsd >= limitUsd)
        {
            var reservedPct = limitUsd <= 0m ? 0m : ClampPct(reservedUsd / limitUsd * 100m);
            return new(period, AssistenteLimiteMensalEstado.Atingido, 100m, reservedPct, 0m, hasReservations, true,
                "Limite mensal atingido.");
        }

        var usedPct = ClampPct(consumedUsd / limitUsd * 100m);
        var reservedPctValid = ClampPct(reservedUsd / limitUsd * 100m);
        var availableUsd = Math.Max(0m, limitUsd - consumedUsd - reservedUsd);
        var availablePct = ClampFloorPct(availableUsd / limitUsd * 100m);

        var committedRatio = (consumedUsd + reservedUsd) / limitUsd;
        var consumedRatio = consumedUsd / limitUsd;

        if (availableUsd <= 0m)
        {
            var stateWhenEmpty = consumedRatio >= 0.90m || committedRatio >= 0.90m
                ? AssistenteLimiteMensalEstado.Critico90
                : consumedRatio >= 0.70m || committedRatio >= 0.70m
                    ? AssistenteLimiteMensalEstado.Atencao70
                    : AssistenteLimiteMensalEstado.Disponivel;
            return new(period, stateWhenEmpty, usedPct, reservedPctValid, 0m, hasReservations, true,
                "Limite disponível insuficiente para esta operação.");
        }

        if (consumedRatio >= 0.90m || committedRatio >= 0.90m)
        {
            return new(period, AssistenteLimiteMensalEstado.Critico90, usedPct, reservedPctValid, availablePct, hasReservations, false,
                $"Restam {FormatPct(availablePct)}% do limite mensal.");
        }

        if (consumedRatio >= 0.70m || committedRatio >= 0.70m)
        {
            var msg = consumedRatio >= 0.70m
                ? $"Você utilizou {FormatPct(usedPct)}% do limite mensal."
                : $"Restam {FormatPct(availablePct)}% do limite mensal.";
            return new(period, AssistenteLimiteMensalEstado.Atencao70, usedPct, reservedPctValid, availablePct, hasReservations, false, msg);
        }

        return new(period, AssistenteLimiteMensalEstado.Disponivel, usedPct, reservedPctValid, availablePct, hasReservations, false, null);
    }

    public async Task ValidarPreCondicaoSolicitacaoAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (IsSimulatedOptOut(settings)) return;
        EnsurePricingValidOrThrow(settings);

        var now = _time.GetUtcNow().UtcDateTime;
        var limitUsd = await ResolveEffectiveLimitUsdAsync(organizationId, settings, cancellationToken);
        var account = await db.AiMonthlyAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.PeriodYear == now.Year && x.PeriodMonth == now.Month, cancellationToken);

        var consumed = account?.ConsumedUsd ?? 0m;
        var reserved = account?.ReservedUsd ?? 0m;

        if (limitUsd <= 0m || consumed >= limitUsd)
            throw LimitReached();

        if (consumed + reserved >= limitUsd)
            throw LimitInsufficient();
    }

    public async Task ValidarDisponibilidadeAsync(
        Guid organizationId,
        int estimatedInputTokens,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (IsSimulatedOptOut(settings)) return;
        EnsurePricingValidOrThrow(settings);

        var conservativeUsd = CalculateConservativeReserveUsd(estimatedInputTokens, maxOutputTokens, settings);
        var now = _time.GetUtcNow().UtcDateTime;
        var limitUsd = await ResolveEffectiveLimitUsdAsync(organizationId, settings, cancellationToken);
        var account = await db.AiMonthlyAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.PeriodYear == now.Year && x.PeriodMonth == now.Month, cancellationToken);

        var consumed = account?.ConsumedUsd ?? 0m;
        var reserved = account?.ReservedUsd ?? 0m;

        if (limitUsd <= 0m || consumed >= limitUsd)
            throw LimitReached();

        if (consumed + reserved + conservativeUsd > limitUsd)
            throw LimitInsufficient();
    }

    public async Task<AssistenteFinancialReservation> ReservarAsync(
        AssistenteFinancialReserveRequest request,
        CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (IsSimulatedOptOut(settings))
        {
            var nowOptOut = _time.GetUtcNow().UtcDateTime;
            return new(Guid.Empty, Guid.Empty, request.OrganizationId, nowOptOut.Year, nowOptOut.Month,
                settings.Provider, settings.Model, settings.PricingVersion,
                settings.InputCostPerMillionTokensUsd, settings.CachedInputCostPerMillionTokensUsd, settings.OutputCostPerMillionTokensUsd,
                0m, IsBypassedSimulation: true);
        }

        EnsurePricingValidOrThrow(settings);
        if (request.AttemptCount <= 0)
            throw new AssistenteIaException(StatusCodes.Status400BadRequest, "AI_INVALID_REQUEST", "Tentativa inválida.");

        var now = _time.GetUtcNow().UtcDateTime;
        var year = now.Year;
        var month = now.Month;
        var conservativeUsd = CalculateConservativeReserveUsd(request.EstimatedInputTokens, request.MaxOutputTokens, settings);
        var reservationId = Guid.NewGuid();

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var newAccountId = Guid.NewGuid();

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO ai_monthly_accounts (id, organization_id, period_year, period_month, limit_usd, consumed_usd, reserved_usd, created_at, updated_at)
                VALUES ({newAccountId}, {request.OrganizationId}, {year}, {month}, {settings.DefaultMonthlyLimitUsd}, 0, 0, {now}, {now})
                ON CONFLICT (organization_id, period_year, period_month) DO NOTHING
                """, cancellationToken);

            var account = await db.AiMonthlyAccounts
                .FromSqlInterpolated($"""
                    SELECT * FROM ai_monthly_accounts
                    WHERE organization_id = {request.OrganizationId} AND period_year = {year} AND period_month = {month}
                    FOR UPDATE
                    """)
                .AsNoTracking()
                .SingleAsync(cancellationToken);

            // Consistent per-organization lock order with DefinirLimiteOrganizacaoAsync:
            // ai_monthly_accounts (current period) -> organizations -> ai_organization_monthly_limits.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM organizations WHERE id = {request.OrganizationId} FOR UPDATE
                """, cancellationToken);

            var effectiveLimit = await ResolveEffectiveLimitUsdAsync(request.OrganizationId, settings, cancellationToken);
            if (account.LimitUsd != effectiveLimit)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE ai_monthly_accounts
                    SET limit_usd = {effectiveLimit}, updated_at = {now}
                    WHERE id = {account.Id}
                    """, cancellationToken);
                account.LimitUsd = effectiveLimit;
            }

            var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE ai_monthly_accounts
                SET reserved_usd = reserved_usd + {conservativeUsd}, updated_at = {now}
                WHERE id = {account.Id}
                  AND consumed_usd + reserved_usd + {conservativeUsd} <= limit_usd
                """, cancellationToken);

            if (updated == 0)
            {
                await tx.RollbackAsync(cancellationToken);
                if (account.LimitUsd <= 0m || account.ConsumedUsd >= account.LimitUsd)
                    throw LimitReached();
                throw LimitInsufficient();
            }

            var entity = new AiUsageReservation
            {
                Id = reservationId,
                AccountId = account.Id,
                OrganizationId = request.OrganizationId,
                UserId = request.UserId,
                SolicitacaoId = request.SolicitacaoId,
                AttemptCount = request.AttemptCount,
                ClientRequestId = request.ClientRequestId,
                CorrelationId = request.CorrelationId,
                PeriodYear = year,
                PeriodMonth = month,
                OperationType = Truncate(request.OperationType, 60),
                Provider = settings.Provider,
                Model = settings.Model,
                PricingVersion = settings.PricingVersion,
                InputCostPerMillionUsd = settings.InputCostPerMillionTokensUsd,
                CachedInputCostPerMillionUsd = settings.CachedInputCostPerMillionTokensUsd,
                OutputCostPerMillionUsd = settings.OutputCostPerMillionTokensUsd,
                EstimatedInputTokens = Math.Max(0, request.EstimatedInputTokens),
                MaxOutputTokens = Math.Max(0, request.MaxOutputTokens),
                ReservedUsd = conservativeUsd,
                Status = "Reserved",
                CreatedAt = now,
                UpdatedAt = now
            };

            db.AiUsageReservations.Add(entity);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                db.ChangeTracker.Clear();
                await tx.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    "Tentativa duplicada de reserva financeira bloqueada. SolicitacaoId={SolicitacaoId} AttemptCount={AttemptCount} OrganizationId={OrganizationId}",
                    request.SolicitacaoId,
                    request.AttemptCount,
                    request.OrganizationId);
                throw new AssistenteIaException(
                    StatusCodes.Status409Conflict,
                    "AI_DUPLICATE_CALL_ATTEMPT",
                    "Esta tentativa de chamada já foi registrada e não pode ser repetida.");
            }

            await tx.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();

            return new(
                reservationId,
                account.Id,
                request.OrganizationId,
                year,
                month,
                settings.Provider,
                settings.Model,
                settings.PricingVersion,
                settings.InputCostPerMillionTokensUsd,
                settings.CachedInputCostPerMillionTokensUsd,
                settings.OutputCostPerMillionTokensUsd,
                conservativeUsd);
        }
        catch (AssistenteIaException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            logger.LogError(
                "Falha de persistência ao reservar limite mensal. SolicitacaoId={SolicitacaoId} OrganizationId={OrganizationId} ErrorType={ErrorType}",
                request.SolicitacaoId,
                request.OrganizationId,
                ex.GetType().Name);
            throw PersistenceFailed(ex);
        }
    }

    public async Task<AssistenteCallSettlementOutcome> LiquidarAsync(
        AssistenteFinancialReservation reservation,
        int inputTokens,
        int? cachedInputTokens,
        int outputTokens,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        if (reservation.IsBypassedSimulation)
            return new("Settled", null, reservation.PricingVersion);

        var safeInput = Math.Max(0, inputTokens);
        var safeCached = Math.Clamp(cachedInputTokens ?? 0, 0, safeInput);
        var safeOutput = Math.Max(0, outputTokens);
        var settledUsd = CalculateActualCostUsd(reservation, safeInput, safeCached, safeOutput);
        var now = _time.GetUtcNow().UtcDateTime;

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var row = await LockReservationAsync(reservation.ReservationId, cancellationToken);
            if (row.Status is "Settled" or "Released")
            {
                await tx.CommitAsync(cancellationToken);
                return new(row.Status, row.SettledUsd, row.PricingVersion);
            }

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE ai_monthly_accounts
                SET reserved_usd = GREATEST(0, reserved_usd - {row.ReservedUsd}),
                    consumed_usd = consumed_usd + {settledUsd},
                    updated_at = {now}
                WHERE id = {row.AccountId}
                """, cancellationToken);

            await db.AiUsageReservations
                .Where(r => r.Id == row.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, "Settled")
                    .SetProperty(r => r.SettledUsd, settledUsd)
                    .SetProperty(r => r.ActualInputTokens, safeInput)
                    .SetProperty(r => r.ActualCachedInputTokens, safeCached)
                    .SetProperty(r => r.ActualOutputTokens, safeOutput)
                    .SetProperty(r => r.ErrorCode, Truncate(errorCode, 80))
                    .SetProperty(r => r.SettledAt, now)
                    .SetProperty(r => r.UpdatedAt, now), cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new("Settled", settledUsd, row.PricingVersion);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                "Falha de persistência ao liquidar reserva de IA. ReservationId={ReservationId} OrganizationId={OrganizationId} ErrorType={ErrorType}",
                reservation.ReservationId,
                reservation.OrganizationId,
                ex.GetType().Name);
            throw PersistenceFailed(ex);
        }
    }

    public async Task LiberarAntesDoEnvioAsync(
        AssistenteFinancialReservation reservation,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (reservation.IsBypassedSimulation) return;
        var now = _time.GetUtcNow().UtcDateTime;

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var row = await LockReservationAsync(reservation.ReservationId, cancellationToken);
            if (row.Status != "Reserved")
            {
                await tx.CommitAsync(cancellationToken);
                return;
            }

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE ai_monthly_accounts
                SET reserved_usd = GREATEST(0, reserved_usd - {row.ReservedUsd}),
                    updated_at = {now}
                WHERE id = {row.AccountId}
                """, cancellationToken);

            await db.AiUsageReservations
                .Where(r => r.Id == row.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, "Released")
                    .SetProperty(r => r.SettledUsd, 0m)
                    .SetProperty(r => r.ErrorCode, Truncate(errorCode, 80))
                    .SetProperty(r => r.SettledAt, now)
                    .SetProperty(r => r.UpdatedAt, now), cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                "Falha de persistência ao liberar reserva antes do envio. ReservationId={ReservationId} OrganizationId={OrganizationId} ErrorType={ErrorType}",
                reservation.ReservationId,
                reservation.OrganizationId,
                ex.GetType().Name);
            throw PersistenceFailed(ex);
        }
    }

    public async Task MarcarIndeterminadaAsync(
        AssistenteFinancialReservation reservation,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (reservation.IsBypassedSimulation) return;
        var now = _time.GetUtcNow().UtcDateTime;

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var row = await LockReservationAsync(reservation.ReservationId, cancellationToken);
            if (row.Status != "Reserved")
            {
                await tx.CommitAsync(cancellationToken);
                return;
            }

            await db.AiUsageReservations
                .Where(r => r.Id == row.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, "Indeterminate")
                    .SetProperty(r => r.ErrorCode, Truncate(errorCode, 80))
                    .SetProperty(r => r.UpdatedAt, now), cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                "Falha de persistência ao marcar reserva indeterminada. ReservationId={ReservationId} OrganizationId={OrganizationId} ErrorType={ErrorType}",
                reservation.ReservationId,
                reservation.OrganizationId,
                ex.GetType().Name);
            throw PersistenceFailed(ex);
        }
    }

    public async Task<AssistenteCallSettlementOutcome> FinalizarChamadaAsync(
        AssistenteFinancialReservation reservation,
        AssistenteProviderCall call,
        CancellationToken cancellationToken)
    {
        if (reservation.IsBypassedSimulation)
            return new("Settled", null, reservation.PricingVersion);

        if (call.InputTokens.HasValue && call.OutputTokens.HasValue)
        {
            return await LiquidarAsync(
                reservation,
                call.InputTokens.Value,
                call.CachedInputTokens,
                call.OutputTokens.Value,
                call.ErrorCode,
                cancellationToken);
        }

        if (!call.Dispatched || call.SafeToRelease)
        {
            await LiberarAntesDoEnvioAsync(reservation, call.ErrorCode ?? "AI_PRE_SEND_ABORTED", cancellationToken);
            return new("Released", 0m, reservation.PricingVersion);
        }

        await MarcarIndeterminadaAsync(reservation, call.ErrorCode ?? "AI_USAGE_INDETERMINATE", cancellationToken);
        return new("Indeterminate", null, reservation.PricingVersion);
    }

    public async Task DefinirLimiteOrganizacaoAsync(
        Guid organizationId,
        decimal monthlyLimitUsd,
        Guid updatedById,
        string reason,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || updatedById == Guid.Empty || monthlyLimitUsd < 0m || string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Informe organizationId, monthlyLimitUsd (>= 0), updatedById e reason válidos.");

        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > 500) trimmedReason = trimmedReason[..500];
        var now = _time.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var orgExists = await db.Organizations.AnyAsync(o => o.Id == organizationId, cancellationToken);
        var userExists = await db.Users.AnyAsync(u => u.Id == updatedById && u.Status == UserStatusEnum.Active, cancellationToken);
        if (!orgExists || !userExists)
            throw new InvalidOperationException("Organização ou usuário operador ativo não encontrado.");

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ai_monthly_accounts (id, organization_id, period_year, period_month, limit_usd, consumed_usd, reserved_usd, created_at, updated_at)
            VALUES ({Guid.NewGuid()}, {organizationId}, {now.Year}, {now.Month}, {monthlyLimitUsd}, 0, 0, {now}, {now})
            ON CONFLICT (organization_id, period_year, period_month) DO NOTHING
            """, cancellationToken);

        await db.AiMonthlyAccounts
            .FromSqlInterpolated($"""
                SELECT * FROM ai_monthly_accounts
                WHERE organization_id = {organizationId} AND period_year = {now.Year} AND period_month = {now.Month}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT id FROM organizations WHERE id = {organizationId} FOR UPDATE
            """, cancellationToken);

        var existing = await db.AiOrganizationMonthlyLimits
            .FromSqlInterpolated($"SELECT * FROM ai_organization_monthly_limits WHERE organization_id = {organizationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        var previousLimit = existing?.MonthlyLimitUsd;
        if (existing is null)
        {
            db.AiOrganizationMonthlyLimits.Add(new AiOrganizationMonthlyLimit
            {
                OrganizationId = organizationId,
                MonthlyLimitUsd = monthlyLimitUsd,
                UpdatedById = updatedById,
                Reason = trimmedReason,
                UpdatedAt = now
            });
        }
        else
        {
            existing.MonthlyLimitUsd = monthlyLimitUsd;
            existing.UpdatedById = updatedById;
            existing.Reason = trimmedReason;
            existing.UpdatedAt = now;
        }

        await db.AiMonthlyAccounts
            .Where(a => a.OrganizationId == organizationId && a.PeriodYear == now.Year && a.PeriodMonth == now.Month)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.LimitUsd, monthlyLimitUsd)
                .SetProperty(a => a.UpdatedAt, now), cancellationToken);

        db.AiFinancialAuditLogs.Add(new AiFinancialAuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Action = "SetMonthlyLimit",
            ActorUserId = updatedById,
            Reason = trimmedReason,
            PreviousLimitUsd = previousLimit,
            NewLimitUsd = monthlyLimitUsd,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public Task<AssistenteReconciliationResult> ReconciliarIndeterminadaAsync(
        Guid reservationId,
        bool confirmarConsumo,
        int? inputTokens,
        int? cachedInputTokens,
        int? outputTokens,
        Guid operatorUserId,
        string reason,
        CancellationToken cancellationToken) =>
        ReconcileCoreAsync(
            reservationId,
            confirmarConsumo ? AssistenteOrphanRecoveryAction.Settle : AssistenteOrphanRecoveryAction.Release,
            allowReserved: false,
            confirmWorkersDrained: true,
            drainEvidence: null,
            inputTokens,
            cachedInputTokens,
            outputTokens,
            operatorUserId,
            reason,
            cancellationToken);

    public Task<AssistenteReconciliationResult> RecuperarReservaOrfaAsync(
        Guid reservationId,
        AssistenteOrphanRecoveryAction action,
        bool confirmWorkersDrained,
        string drainEvidence,
        int? inputTokens,
        int? cachedInputTokens,
        int? outputTokens,
        Guid operatorUserId,
        string reason,
        CancellationToken cancellationToken) =>
        ReconcileCoreAsync(
            reservationId,
            action,
            allowReserved: true,
            confirmWorkersDrained,
            drainEvidence,
            inputTokens,
            cachedInputTokens,
            outputTokens,
            operatorUserId,
            reason,
            cancellationToken);

    private async Task<AssistenteReconciliationResult> ReconcileCoreAsync(
        Guid reservationId,
        AssistenteOrphanRecoveryAction action,
        bool allowReserved,
        bool confirmWorkersDrained,
        string? drainEvidence,
        int? inputTokens,
        int? cachedInputTokens,
        int? outputTokens,
        Guid operatorUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (reservationId == Guid.Empty || operatorUserId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Informe reservationId, operatorUserId e reason válidos.");
        if (allowReserved && (!confirmWorkersDrained || string.IsNullOrWhiteSpace(drainEvidence)))
            throw new InvalidOperationException("A recuperação de reserva órfã exige confirmação explícita de drenagem dos workers (confirmWorkersDrained=true) e evidência operacional.");

        var auditReason = allowReserved
            ? Truncate($"[drain_confirmed=true; evidence={drainEvidence!.Trim()}] {reason.Trim()}", 500)!
            : Truncate(reason.Trim(), 500)!;
        var now = _time.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var operatorExists = await db.Users.AnyAsync(u => u.Id == operatorUserId && u.Status == UserStatusEnum.Active, cancellationToken);
        if (!operatorExists)
            throw new InvalidOperationException("Usuário operador ativo não encontrado.");

        var row = await LockReservationAsync(reservationId, cancellationToken);
        var targetStatus = action switch
        {
            AssistenteOrphanRecoveryAction.MarkIndeterminate => "Indeterminate",
            AssistenteOrphanRecoveryAction.Settle => "Settled",
            AssistenteOrphanRecoveryAction.Release => "Released",
            _ => throw new InvalidOperationException("Ação de reconciliação inválida.")
        };

        if (row.Status == targetStatus && (action != AssistenteOrphanRecoveryAction.MarkIndeterminate || !allowReserved || row.ReconciledById is not null))
        {
            await tx.CommitAsync(cancellationToken);
            return new(row.Id, row.OrganizationId, row.PeriodYear, row.PeriodMonth, row.Status, row.SettledUsd, row.ReservedUsd);
        }

        if (action == AssistenteOrphanRecoveryAction.MarkIndeterminate)
        {
            if (row.Status != "Reserved")
                throw new InvalidOperationException($"A reserva {reservationId} está no estado '{row.Status}' e não pode ser marcada como indeterminada.");

            await db.AiUsageReservations
                .Where(r => r.Id == row.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, "Indeterminate")
                    .SetProperty(r => r.ReconciledById, operatorUserId)
                    .SetProperty(r => r.ReconciliationReason, auditReason)
                    .SetProperty(r => r.UpdatedAt, now), cancellationToken);

            db.AiFinancialAuditLogs.Add(new AiFinancialAuditLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = row.OrganizationId,
                Action = "MarkIndeterminate",
                ReservationId = row.Id,
                ActorUserId = operatorUserId,
                Reason = auditReason,
                CreatedAt = now
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new(row.Id, row.OrganizationId, row.PeriodYear, row.PeriodMonth, "Indeterminate", null, row.ReservedUsd);
        }

        var canTransition = allowReserved ? row.Status is "Reserved" or "Indeterminate" : row.Status == "Indeterminate";
        if (!canTransition)
            throw new InvalidOperationException($"A reserva {reservationId} está no estado '{row.Status}' e não pode ser reconciliada para '{targetStatus}'.");

        decimal settledUsd;
        int? actualIn = null;
        int? actualCached = null;
        int? actualOut = null;

        if (action == AssistenteOrphanRecoveryAction.Settle)
        {
            if (inputTokens.HasValue || outputTokens.HasValue)
            {
                if (!inputTokens.HasValue || !outputTokens.HasValue || inputTokens < 0 || outputTokens < 0 || (cachedInputTokens ?? 0) < 0)
                    throw new InvalidOperationException("Informe inputTokens e outputTokens válidos para liquidar pelo uso real.");
                actualIn = inputTokens.Value;
                actualCached = Math.Clamp(cachedInputTokens ?? 0, 0, actualIn.Value);
                actualOut = outputTokens.Value;
                var uncached = actualIn.Value - actualCached.Value;
                settledUsd = Math.Round(
                    (uncached * row.InputCostPerMillionUsd
                     + actualCached.Value * row.CachedInputCostPerMillionUsd
                     + actualOut.Value * row.OutputCostPerMillionUsd) / 1_000_000m,
                    8, MidpointRounding.AwayFromZero);
            }
            else
            {
                settledUsd = row.ReservedUsd;
            }

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE ai_monthly_accounts
                SET reserved_usd = GREATEST(0, reserved_usd - {row.ReservedUsd}),
                    consumed_usd = consumed_usd + {settledUsd},
                    updated_at = {now}
                WHERE id = {row.AccountId}
                """, cancellationToken);
        }
        else
        {
            settledUsd = 0m;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE ai_monthly_accounts
                SET reserved_usd = GREATEST(0, reserved_usd - {row.ReservedUsd}),
                    updated_at = {now}
                WHERE id = {row.AccountId}
                """, cancellationToken);
        }

        await db.AiUsageReservations
            .Where(r => r.Id == row.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, targetStatus)
                .SetProperty(r => r.SettledUsd, settledUsd)
                .SetProperty(r => r.ActualInputTokens, actualIn)
                .SetProperty(r => r.ActualCachedInputTokens, actualCached)
                .SetProperty(r => r.ActualOutputTokens, actualOut)
                .SetProperty(r => r.ReconciledById, operatorUserId)
                .SetProperty(r => r.ReconciliationReason, auditReason)
                .SetProperty(r => r.SettledAt, now)
                .SetProperty(r => r.UpdatedAt, now), cancellationToken);

        db.AiFinancialAuditLogs.Add(new AiFinancialAuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = row.OrganizationId,
            Action = action == AssistenteOrphanRecoveryAction.Settle ? "ReconcileSettle" : "ReconcileRelease",
            ReservationId = row.Id,
            ActorUserId = operatorUserId,
            Reason = auditReason,
            SettledUsd = settledUsd,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return new(row.Id, row.OrganizationId, row.PeriodYear, row.PeriodMonth, targetStatus, settledUsd, row.ReservedUsd);
    }

    private async Task<AiUsageReservation> LockReservationAsync(Guid reservationId, CancellationToken cancellationToken) =>
        await db.AiUsageReservations
            .FromSqlInterpolated($"SELECT * FROM ai_usage_reservations WHERE id = {reservationId} FOR UPDATE")
            .AsNoTracking()
            .SingleAsync(cancellationToken);

    private async Task<decimal> ResolveEffectiveLimitUsdAsync(
        Guid organizationId,
        AssistenteIaSettings settings,
        CancellationToken cancellationToken)
    {
        var orgOverride = await db.AiOrganizationMonthlyLimits.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => (decimal?)x.MonthlyLimitUsd)
            .SingleOrDefaultAsync(cancellationToken);
        return orgOverride ?? settings.DefaultMonthlyLimitUsd;
    }

    private bool IsSimulatedOptOut(AssistenteIaSettings settings) =>
        !environment.IsProduction() && settings.AllowSimulatedWithoutFinancialControl;

    private static bool IsPricingValid(AssistenteIaSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.Model) &&
        !string.IsNullOrWhiteSpace(settings.PricingVersion) &&
        !string.Equals(settings.PricingVersion.Trim(), "unconfigured", StringComparison.OrdinalIgnoreCase) &&
        settings.DefaultMonthlyLimitUsd >= 0m &&
        settings.InputCostPerMillionTokensUsd > 0m &&
        settings.CachedInputCostPerMillionTokensUsd > 0m &&
        settings.OutputCostPerMillionTokensUsd > 0m;

    private static void EnsurePricingValidOrThrow(AssistenteIaSettings settings)
    {
        if (!IsPricingValid(settings))
        {
            throw new AssistenteIaException(
                StatusCodes.Status503ServiceUnavailable,
                "AI_LIMIT_CONFIGURATION_INVALID",
                "O assistente de IA está temporariamente indisponível.");
        }
    }

    public static decimal CalculateConservativeReserveUsd(int inputTokens, int maxOutputTokens, AssistenteIaSettings settings)
    {
        var safeInput = Math.Max(0, inputTokens);
        var safeOutput = Math.Max(0, maxOutputTokens);
        var worstInputRate = Math.Max(settings.InputCostPerMillionTokensUsd, settings.CachedInputCostPerMillionTokensUsd);
        var raw = (safeInput * worstInputRate + safeOutput * settings.OutputCostPerMillionTokensUsd) / 1_000_000m;
        return Math.Ceiling(raw * 100_000_000m) / 100_000_000m;
    }

    private static decimal CalculateActualCostUsd(
        AssistenteFinancialReservation reservation,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens)
    {
        var uncached = Math.Max(0, inputTokens - cachedInputTokens);
        var raw = (uncached * reservation.InputCostPerMillionUsd
                   + cachedInputTokens * reservation.CachedInputCostPerMillionUsd
                   + outputTokens * reservation.OutputCostPerMillionUsd) / 1_000_000m;
        return Math.Round(raw, 8, MidpointRounding.AwayFromZero);
    }

    private static decimal ClampPct(decimal value) =>
        Math.Clamp(Math.Round(value, 1, MidpointRounding.AwayFromZero), 0m, 100m);

    private static decimal ClampFloorPct(decimal value) =>
        Math.Clamp(Math.Floor(value * 10m) / 10m, 0m, 100m);

    private static string FormatPct(decimal value) =>
        value % 1m == 0m
            ? ((int)value).ToString(PtBr)
            : value.ToString("0.#", PtBr);

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= maxLength ? value : value[..maxLength];

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static AssistenteIaException LimitReached()
    {
        AssistenteTelemetry.Shared.OperationalEvent("AI_MONTHLY_LIMIT_REACHED");
        return new(StatusCodes.Status429TooManyRequests, "AI_MONTHLY_LIMIT_REACHED", "Limite mensal atingido.");
    }

    private static AssistenteIaException LimitInsufficient()
    {
        AssistenteTelemetry.Shared.OperationalEvent("AI_MONTHLY_LIMIT_INSUFFICIENT");
        return new(StatusCodes.Status429TooManyRequests, "AI_MONTHLY_LIMIT_INSUFFICIENT", "Limite disponível insuficiente para esta operação.");
    }

    private static AssistenteIaException PersistenceFailed(Exception inner) =>
        new(StatusCodes.Status503ServiceUnavailable, "AI_USAGE_PERSISTENCE_FAILED",
            "Não foi possível registrar o consumo da solicitação. Novas chamadas foram bloqueadas por segurança.",
            retryable: true, innerException: inner);
}
