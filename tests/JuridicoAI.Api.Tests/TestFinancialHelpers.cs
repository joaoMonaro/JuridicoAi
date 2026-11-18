using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace JuridicoAI.Api.Tests;

internal sealed class TestHostEnvironment(string environmentName = "Testing") : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "JuridicoAI.Api.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class SimulatedMonthlyLimitStub : IAssistenteMonthlyLimitService
{
    public bool ControleFinanceiroAtivo => false;

    public Task<AssistenteLimiteMensalDto> ObterResumoAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(new AssistenteLimiteMensalDto("2026-11", AssistenteLimiteMensalEstado.Disponivel, 0m, 0m, 100m, false, false, null));

    public Task ValidarPreCondicaoSolicitacaoAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task ValidarDisponibilidadeAsync(Guid organizationId, int estimatedInputTokens, int maxOutputTokens, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<AssistenteFinancialReservation> ReservarAsync(AssistenteFinancialReserveRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new AssistenteFinancialReservation(Guid.Empty, Guid.Empty, request.OrganizationId, 2026, 11,
            "OpenAI", "simulado", "simulado", 0m, 0m, 0m, 0m, IsBypassedSimulation: true));

    public Task<AssistenteCallSettlementOutcome> LiquidarAsync(AssistenteFinancialReservation reservation, int inputTokens, int? cachedInputTokens, int outputTokens, string? errorCode, CancellationToken cancellationToken) =>
        Task.FromResult(new AssistenteCallSettlementOutcome("Settled", null, reservation.PricingVersion));

    public Task LiberarAntesDoEnvioAsync(AssistenteFinancialReservation reservation, string errorCode, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task MarcarIndeterminadaAsync(AssistenteFinancialReservation reservation, string errorCode, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<AssistenteCallSettlementOutcome> FinalizarChamadaAsync(AssistenteFinancialReservation reservation, AssistenteProviderCall call, CancellationToken cancellationToken) =>
        Task.FromResult(new AssistenteCallSettlementOutcome("Settled", null, reservation.PricingVersion));

    public Task DefinirLimiteOrganizacaoAsync(Guid organizationId, decimal monthlyLimitUsd, Guid updatedById, string reason, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<AssistenteReconciliationResult> ReconciliarIndeterminadaAsync(Guid reservationId, bool confirmarConsumo, int? inputTokens, int? cachedInputTokens, int? outputTokens, Guid operatorUserId, string reason, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<AssistenteReconciliationResult> RecuperarReservaOrfaAsync(Guid reservationId, AssistenteOrphanRecoveryAction action, bool confirmWorkersDrained, string drainEvidence, int? inputTokens, int? cachedInputTokens, int? outputTokens, Guid operatorUserId, string reason, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
