using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Controllers.Assistente;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Documents;
using JuridicoAI.Api.Infrastructure.Http;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JuridicoAI.Api.Tests;

public sealed class AssistenteMonthlyLimitPostgresTests
{
    private static AssistenteIaSettings PricedSettings(decimal monthlyLimitUsd = 5.00m) => new()
    {
        Enabled = true,
        ReviewV2Enabled = true,
        ConversationV2Enabled = true,
        DurableGenerationEnabled = true,
        HistoryMemoryEnabled = true,
        Model = "gpt-test-p03",
        ApiKey = "chave-simulada-p03",
        PricingVersion = "pricing-p03-v1",
        DefaultMonthlyLimitUsd = monthlyLimitUsd,
        InputCostPerMillionTokensUsd = 2.00m,
        CachedInputCostPerMillionTokensUsd = 0.50m,
        OutputCostPerMillionTokensUsd = 8.00m,
        MaxInputTokens = 60_000,
        MaxOutputTokens = 1_000,
        AllowSimulatedWithoutFinancialControl = false
    };

    [ReviewPostgresFact]
    public async Task CorridaEntreInstancias_ReservaAtomicaNoPostgreSQLNuncaExcedeLimiteECriaContaSemDuplicacao()
    {
        await using var db = await P03Database.CreateAsync();
        // Limit = 0.01500000 USD. Each call with 1000 input + 1000 max output costs:
        // (1000 * 2 + 1000 * 8) / 1_000_000 = 0.01000000 USD.
        // Only ONE of 6 concurrent instances can reserve 0.01 USD inside 0.015 USD!
        var settings = PricedSettings(monthlyLimitUsd: 0.015m);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(1, 6).Select(async i =>
        {
            await using var ctx = db.Context();
            var svc = db.LimitService(ctx, settings);
            await gate.Task;
            try
            {
                var res = await svc.ReservarAsync(new AssistenteFinancialReserveRequest(
                    db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(),
                    "Concurrent", 1_000, 1_000), default);
                return (Succeeded: true, Code: (string?)null, Reservation: (AssistenteFinancialReservation?)res);
            }
            catch (AssistenteIaException ex)
            {
                return (Succeeded: false, Code: (string?)ex.Code, Reservation: (AssistenteFinancialReservation?)null);
            }
        }).ToArray();

        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r.Succeeded);
        Assert.Equal(5, results.Count(r => !r.Succeeded && r.Code == "AI_MONTHLY_LIMIT_INSUFFICIENT"));

        await using var verify = db.Context();
        var account = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.Equal(0.015m, account.LimitUsd);
        Assert.Equal(0m, account.ConsumedUsd);
        Assert.Equal(0.01m, account.ReservedUsd);
        Assert.True(account.ConsumedUsd + account.ReservedUsd <= account.LimitUsd);
        Assert.Single(await verify.AiUsageReservations.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task IsolacaoEntreOrganizacoes_EsgotamentoDeUmaOrganizacaoNaoBloqueiaOutra()
    {
        await using var db = await P03Database.CreateAsync();
        var orgB = await db.CreateSecondOrganizationAsync();
        var settings = PricedSettings(monthlyLimitUsd: 0.01m);

        await using (var ctxA = db.Context())
        {
            var svcA = db.LimitService(ctxA, settings);
            var resA = await svcA.ReservarAsync(new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Op", 1_000, 1_000), default);
            await svcA.LiquidarAsync(resA, 1_000, 0, 1_000, null, default);

            var exA = await Assert.ThrowsAsync<AssistenteIaException>(() =>
                svcA.ReservarAsync(new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Op", 1_000, 1_000), default));
            Assert.Equal("AI_MONTHLY_LIMIT_REACHED", exA.Code);
        }

        await using (var ctxB = db.Context())
        {
            var svcB = db.LimitService(ctxB, settings);
            var summaryB = await svcB.ObterResumoAsync(orgB, default);
            Assert.Equal(0m, summaryB.PercentualUtilizado);
            Assert.Equal(100m, summaryB.PercentualDisponivel);
            Assert.False(summaryB.BloqueadoParaNovasChamadas);

            var resB = await svcB.ReservarAsync(new(orgB, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Op", 500, 500), default);
            Assert.Equal(orgB, resB.OrganizationId);
        }
    }

    [ReviewPostgresFact]
    public async Task ViradaDeMesUtc_ReservaAntigaPermaneceNoMesDeOrigemENovoMesIniciaZerado()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 0.02m);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 30, 23, 59, 50, TimeSpan.Zero));

        AssistenteFinancialReservation novReservation;
        await using (var ctx = db.Context())
        {
            var svc = db.LimitService(ctx, settings, clock);
            novReservation = await svc.ReservarAsync(new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Turn", 1_000, 1_000), default);
            Assert.Equal(2026, novReservation.PeriodYear);
            Assert.Equal(11, novReservation.PeriodMonth);
        }

        // Cross UTC midnight into December 2026
        clock.Advance(TimeSpan.FromSeconds(20));

        await using (var ctxDec = db.Context())
        {
            var svcDec = db.LimitService(ctxDec, settings, clock);
            var decSummary = await svcDec.ObterResumoAsync(db.OrgId, default);
            Assert.Equal("2026-12", decSummary.PeriodoReferencia);
            Assert.Equal(0m, decSummary.PercentualUtilizado);
            Assert.Equal(0m, decSummary.PercentualReservado);
            Assert.Equal(100m, decSummary.PercentualDisponivel);

            // Settle November reservation AFTER UTC month rollover
            await svcDec.LiquidarAsync(novReservation, 1_000, 500, 250, null, default);

            // December remains untouched
            var decAfter = await svcDec.ObterResumoAsync(db.OrgId, default);
            Assert.Equal(0m, decAfter.PercentualUtilizado);
            Assert.Equal(0m, decAfter.PercentualReservado);

            // Reserve in December creates separate December account
            await svcDec.ReservarAsync(new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "DecOp", 1_000, 1_000), default);
        }

        await using var verify = db.Context();
        var novAccount = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId && a.PeriodYear == 2026 && a.PeriodMonth == 11);
        var decAccount = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId && a.PeriodYear == 2026 && a.PeriodMonth == 12);
        // Nov: uncached=500 ($0.001) + cached=500 ($0.00025) + output=250 ($0.002) = 0.00325 USD
        Assert.Equal(0.00325m, novAccount.ConsumedUsd);
        Assert.Equal(0m, novAccount.ReservedUsd);
        Assert.Equal(0m, decAccount.ConsumedUsd);
        Assert.Equal(0.01m, decAccount.ReservedUsd);
    }

    [ReviewPostgresFact]
    public async Task FaltaDeSaldo_BloqueiaAntesDeChamarProvedorRealSemCustosOuLedger()
    {
        await using var db = await P03Database.CreateAsync();
        // Reserve/consume 0.009 USD of 0.010 USD limit, leaving 0.001 USD available.
        // Next call needs 1000 input + 1000 max output = 0.010 USD -> insufficient BEFORE calling provider!
        var settings = PricedSettings(monthlyLimitUsd: 0.01m);
        var providerCalls = 0;
        var handler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":1000}""") });
            providerCalls++;
            return Task.FromResult(MemoryStream("""{"titulo":"R","texto":"T","resumo":"","operacaoSugerida":"ResponderNoPainel","alertas":[],"achados":[]}""", 100, 0, 50));
        });

        await using (var setupCtx = db.Context())
        {
            var limitSvc = db.LimitService(setupCtx, settings);
            var r = await limitSvc.ReservarAsync(new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Prev", 1_000, 1_000), default);
            // Settle 0.009 USD (1000 input uncached = 0.002, 875 output = 0.007 => 0.009)
            await limitSvc.LiquidarAsync(r, 1_000, 0, 875, null, default);
        }

        await using var ctx = db.Context();
        var app = db.AppService(ctx, settings, handler);
        var request = new CriarSolicitacaoAssistenteRequest(
            Guid.NewGuid(), AssistenteModo.Perguntar, "Analise", 1, AssistenteOperacao.ResponderNoPainel,
            new(new(AssistenteEscopoDocumento.Nenhum)));

        var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => app.PrepararAsync(db.PecaId, request, default));
        Assert.Equal("AI_MONTHLY_LIMIT_INSUFFICIENT", ex.Code);
        Assert.Equal(429, ex.StatusCode);
        Assert.Equal("Limite disponível insuficiente para esta operação.", ex.Message);
        Assert.Equal(0, providerCalls);
        Assert.Empty(await ctx.AiUsageLedger.AsNoTracking().ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task LiquidacaoEReconciliacaoRepetidasEChaveDuplicada_NaoDuplicamGastoNemLiberamDuasVezes()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 5.00m);
        var solicitationId = Guid.NewGuid();

        AssistenteFinancialReservation reservation;
        await using (var ctx = db.Context())
        {
            var svc = db.LimitService(ctx, settings);
            reservation = await svc.ReservarAsync(new(db.OrgId, db.UserId, solicitationId, 1, Guid.NewGuid(), Guid.NewGuid(), "Op", 1_000, 1_000), default);

            // Duplicate physical attempt key (SolicitacaoId, AttemptCount=1) MUST be rejected and NOT reserve again!
            var dupEx = await Assert.ThrowsAsync<AssistenteIaException>(() =>
                svc.ReservarAsync(new(db.OrgId, db.UserId, solicitationId, 1, Guid.NewGuid(), Guid.NewGuid(), "Op", 1_000, 1_000), default));
            Assert.Equal("AI_DUPLICATE_CALL_ATTEMPT", dupEx.Code);

            // Repeated settlement is idempotent
            var first = await svc.LiquidarAsync(reservation, 1_000, 200, 500, null, default);
            var second = await svc.LiquidarAsync(reservation, 1_000, 200, 500, null, default);
            Assert.Equal("Settled", first.Status);
            Assert.Equal(first.SettledUsd, second.SettledUsd);

            // Pre-send release after already settled is a no-op
            await svc.LiberarAntesDoEnvioAsync(reservation, "AI_CANCELLED", default);
        }

        await using var verify = db.Context();
        var account = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
        // uncached = 800 * 2 / 1e6 = 0.0016; cached = 200 * 0.5 / 1e6 = 0.0001; output = 500 * 8 / 1e6 = 0.004 => 0.0057
        Assert.Equal(0.0057m, account.ConsumedUsd);
        Assert.Equal(0m, account.ReservedUsd);
    }

    [ReviewPostgresFact]
    public async Task IndeterminadaAposDespacho_MantemReservaAposReinicioEPermiteReconciliacaoAuditada()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 0.015m);

        var handler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":1000}""") });
            // Simulate 502/timeout after dispatch with no usage
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
        });

        await using (var ctx = db.Context())
        {
            var app = db.AppService(ctx, settings, handler);
            var exec = await app.PrepararAsync(db.PecaId, new CriarSolicitacaoAssistenteRequest(
                Guid.NewGuid(), AssistenteModo.Perguntar, "Pergunta", 1, AssistenteOperacao.ResponderNoPainel,
                new(new(AssistenteEscopoDocumento.Nenhum))), default);
            try
            {
                var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => app.GerarAsync(exec, default));
                Assert.Equal("AI_UNAVAILABLE", ex.Code);
            }
            finally { await app.FinalizarAsync(exec); }
        }

        // Simulate restart with brand-new DbContext
        Guid reservationId;
        await using (var afterRestart = db.Context())
        {
            var res = await afterRestart.AiUsageReservations.SingleAsync();
            Assert.Equal("Indeterminate", res.Status);
            Assert.Equal(0.01m, res.ReservedUsd);
            reservationId = res.Id;

            var account = await afterRestart.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
            Assert.Equal(0m, account.ConsumedUsd);
            Assert.Equal(0.01m, account.ReservedUsd);

            // Available is 0.005 < 0.010, so next call is blocked!
            var svc = db.LimitService(afterRestart, settings);
            var summary = await svc.ObterResumoAsync(db.OrgId, default);
            Assert.Equal(0m, summary.PercentualUtilizado);
            Assert.True(summary.PossuiReservasEmAndamento);
            Assert.True(summary.PercentualReservado > 0m);

            // Reconcile via audited operational service: release after verifying no provider charge
            var reconciled = await svc.ReconciliarIndeterminadaAsync(
                reservationId, confirmarConsumo: false, null, null, null, db.UserId,
                "Verificado no painel do provedor: erro 502 sem faturamento.", default);
            Assert.Equal("Released", reconciled.Status);

            // Re-running reconciliation is idempotent
            var reconciledAgain = await svc.ReconciliarIndeterminadaAsync(
                reservationId, confirmarConsumo: false, null, null, null, db.UserId,
                "Reexecução idempotente.", default);
            Assert.Equal("Released", reconciledAgain.Status);
        }

        await using var verify = db.Context();
        var finalAccount = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.Equal(0m, finalAccount.ReservedUsd);
        Assert.Equal(0m, finalAccount.ConsumedUsd);
        Assert.Single(await verify.AiFinancialAuditLogs.Where(a => a.ReservationId == reservationId).ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task FalhaComprovadamenteAntesDoEnvio_LiberaReservaComSeguranca()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 5.00m);

        using var cts = new CancellationTokenSource();
        var sendCalls = 0;
        var handler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":1000}""") });
            sendCalls++;
            throw new InvalidOperationException("Não deveria ter despachado quando cancelado antes do envio.");
        });

        await using (var ctx = db.Context())
        {
            var realLimit = db.LimitService(ctx, settings);
            var cancelAfterReserve = new CancelAfterReserveDecorator(realLimit, cts);
            var provider = new OpenAiAssistenteIaProvider(new HttpClient(handler) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(settings));
            var auth = new TestAuthContext(db.OrgId, db.UserId);
            var monitor = new Monitor(settings);
            var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(ctx), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
            var app = new AssistenteIaApplicationService(
                new PecaRepository(ctx), new CasoRepository(ctx), auth, provider, provider,
                new AssistenteContextBuilder(Options.Create(settings)), new AssistentePromptPolicy(),
                new AssistenteRequestRegistry(), db.Access, new Transparency(),
                new AssistenteUsageLimiter(monitor), usage, cancelAfterReserve, new CorrelationStub(),
                monitor, NullLogger<AssistenteIaApplicationService>.Instance);

            var exec = await app.PrepararAsync(db.PecaId, new CriarSolicitacaoAssistenteRequest(
                Guid.NewGuid(), AssistenteModo.Perguntar, "Pergunta", 1, AssistenteOperacao.ResponderNoPainel,
                new(new(AssistenteEscopoDocumento.Nenhum))), default);
            try
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => app.GerarAsync(exec, cts.Token));
            }
            finally { await app.FinalizarAsync(exec); }
        }

        Assert.Equal(0, sendCalls);
        await using var verify = db.Context();
        var res = await verify.AiUsageReservations.SingleAsync();
        Assert.Equal("Released", res.Status);
        var account = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.Equal(0m, account.ReservedUsd);
        Assert.Equal(0m, account.ConsumedUsd);
    }

    [ReviewPostgresFact]
    public async Task FalhaDePersistenciaNaLiquidacao_ImpedeProximasChamadasENaoMascaraComoGratuito()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 5.00m);
        var sendCalls = 0;

        var handler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":500}""") });
            sendCalls++;
            var callJson = JsonSerializer.Serialize(new { type = "function_call", call_id = $"call-{sendCalls}", name = "listar_fontes", arguments = """{"aposIndice":0,"limite":5,"coberturaPretendida":"Relevantes"}""" });
            return Task.FromResult(MemoryStreamTool(callJson, 500, 0, 100));
        });

        await using (var ctx = db.Context())
        {
            var realLimit = db.LimitService(ctx, settings);
            var failingSettlement = new FailSettlementDecorator(realLimit);
            var docSettings = Options.Create(new AssistenteDocumentSettings { Enabled = true, RetrievalEnabled = true });
            var repo = new AssistenteWorkspaceRepository(ctx);
            var pieces = new PecaRepository(ctx);
            var auth = new TestAuthContext(db.OrgId, db.UserId);
            var monitor = new Monitor(settings);
            var docs = new AssistenteDocumentService(ctx, repo, pieces, new InMemoryStorage(), auth, db.Access, new Transparency(), docSettings);
            var chat = new AssistenteChatService(repo, pieces, auth, db.Access, new Transparency(), failingSettlement, monitor, docs);
            var provider = new OpenAiAssistenteIaProvider(new HttpClient(handler) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(settings));
            var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(ctx), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
            var app = new AssistenteIaApplicationService(
                pieces, new CasoRepository(ctx), auth, provider, provider,
                new AssistenteContextBuilder(Options.Create(settings)), new AssistentePromptPolicy(),
                new AssistenteRequestRegistry(), db.Access, new Transparency(),
                new AssistenteUsageLimiter(monitor), usage, failingSettlement, new CorrelationStub(),
                monitor, NullLogger<AssistenteIaApplicationService>.Instance, docSettings);

            var conv = await new AssistenteWorkspaceService(repo, pieces, auth, db.Access, new Transparency(), monitor, new AssistentePatchCompiler(), docs)
                .ObterOuCriarAsync(db.PecaId, default);
            var req = new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Consulte os anexos.", 1).Solicitar();
            var start = await chat.IniciarAsync(conv.Id, req, default);
            var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask);
            var exec = await app.PrepararAsync(db.PecaId, req, default, start.Contexto, conversaLivre: true, tools);
            try
            {
                var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => app.GerarAsync(exec, default));
                Assert.Equal("AI_USAGE_PERSISTENCE_FAILED", ex.Code);
            }
            finally { await app.FinalizarAsync(exec); }
        }

        // Stopped after round 1; round 2 was NEVER called!
        Assert.Equal(1, sendCalls);
        // Conservative reservation from round 1 remains held (never masked as free!)
        await using var verify = db.Context();
        var account = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.True(account.ReservedUsd > 0m);
        var res = await verify.AiUsageReservations.SingleAsync();
        Assert.Equal("Reserved", res.Status);
    }

    private sealed class CancelAfterReserveDecorator(IAssistenteMonthlyLimitService inner, CancellationTokenSource cts) : IAssistenteMonthlyLimitService
    {
        public bool ControleFinanceiroAtivo => inner.ControleFinanceiroAtivo;
        public Task<AssistenteLimiteMensalDto> ObterResumoAsync(Guid organizationId, CancellationToken ct) => inner.ObterResumoAsync(organizationId, ct);
        public Task ValidarPreCondicaoSolicitacaoAsync(Guid organizationId, CancellationToken ct) => inner.ValidarPreCondicaoSolicitacaoAsync(organizationId, ct);
        public Task ValidarDisponibilidadeAsync(Guid organizationId, int estimatedInputTokens, int maxOutputTokens, CancellationToken ct) => inner.ValidarDisponibilidadeAsync(organizationId, estimatedInputTokens, maxOutputTokens, ct);
        public async Task<AssistenteFinancialReservation> ReservarAsync(AssistenteFinancialReserveRequest request, CancellationToken ct)
        {
            var res = await inner.ReservarAsync(request, ct);
            cts.Cancel();
            return res;
        }
        public Task<AssistenteCallSettlementOutcome> LiquidarAsync(AssistenteFinancialReservation reservation, int inputTokens, int? cachedInputTokens, int outputTokens, string? errorCode, CancellationToken ct, int? cacheWriteTokens = null) => inner.LiquidarAsync(reservation, inputTokens, cachedInputTokens, outputTokens, errorCode, ct, cacheWriteTokens);
        public Task LiberarAntesDoEnvioAsync(AssistenteFinancialReservation reservation, string errorCode, CancellationToken ct) => inner.LiberarAntesDoEnvioAsync(reservation, errorCode, ct);
        public Task MarcarIndeterminadaAsync(AssistenteFinancialReservation reservation, string errorCode, CancellationToken ct) => inner.MarcarIndeterminadaAsync(reservation, errorCode, ct);
        public Task<AssistenteCallSettlementOutcome> FinalizarChamadaAsync(AssistenteFinancialReservation reservation, AssistenteProviderCall call, CancellationToken ct) => inner.FinalizarChamadaAsync(reservation, call, ct);
        public Task DefinirLimiteOrganizacaoAsync(Guid organizationId, decimal monthlyLimitUsd, Guid updatedById, string reason, CancellationToken ct) => inner.DefinirLimiteOrganizacaoAsync(organizationId, monthlyLimitUsd, updatedById, reason, ct);
        public Task<AssistenteReconciliationResult> ReconciliarIndeterminadaAsync(Guid reservationId, bool confirmarConsumo, int? inputTokens, int? cachedInputTokens, int? outputTokens, Guid operatorUserId, string reason, CancellationToken ct, int? cacheWriteTokens = null) => inner.ReconciliarIndeterminadaAsync(reservationId, confirmarConsumo, inputTokens, cachedInputTokens, outputTokens, operatorUserId, reason, ct, cacheWriteTokens);
        public Task<AssistenteReconciliationResult> RecuperarReservaOrfaAsync(Guid reservationId, AssistenteOrphanRecoveryAction action, bool confirmWorkersDrained, string drainEvidence, int? inputTokens, int? cachedInputTokens, int? outputTokens, Guid operatorUserId, string reason, CancellationToken ct, int? cacheWriteTokens = null) => inner.RecuperarReservaOrfaAsync(reservationId, action, confirmWorkersDrained, drainEvidence, inputTokens, cachedInputTokens, outputTokens, operatorUserId, reason, ct, cacheWriteTokens);
    }

    private sealed class FailSettlementDecorator(IAssistenteMonthlyLimitService inner) : IAssistenteMonthlyLimitService
    {
        public bool ControleFinanceiroAtivo => inner.ControleFinanceiroAtivo;
        public Task<AssistenteLimiteMensalDto> ObterResumoAsync(Guid organizationId, CancellationToken ct) => inner.ObterResumoAsync(organizationId, ct);
        public Task ValidarPreCondicaoSolicitacaoAsync(Guid organizationId, CancellationToken ct) => inner.ValidarPreCondicaoSolicitacaoAsync(organizationId, ct);
        public Task ValidarDisponibilidadeAsync(Guid organizationId, int estimatedInputTokens, int maxOutputTokens, CancellationToken ct) => inner.ValidarDisponibilidadeAsync(organizationId, estimatedInputTokens, maxOutputTokens, ct);
        public Task<AssistenteFinancialReservation> ReservarAsync(AssistenteFinancialReserveRequest request, CancellationToken ct) => inner.ReservarAsync(request, ct);
        public Task<AssistenteCallSettlementOutcome> LiquidarAsync(AssistenteFinancialReservation reservation, int inputTokens, int? cachedInputTokens, int outputTokens, string? errorCode, CancellationToken ct, int? cacheWriteTokens = null) =>
            throw new AssistenteIaException(503, "AI_USAGE_PERSISTENCE_FAILED", "Falha sintética de persistência.", retryable: true);
        public Task LiberarAntesDoEnvioAsync(AssistenteFinancialReservation reservation, string errorCode, CancellationToken ct) => inner.LiberarAntesDoEnvioAsync(reservation, errorCode, ct);
        public Task MarcarIndeterminadaAsync(AssistenteFinancialReservation reservation, string errorCode, CancellationToken ct) => inner.MarcarIndeterminadaAsync(reservation, errorCode, ct);
        public Task<AssistenteCallSettlementOutcome> FinalizarChamadaAsync(AssistenteFinancialReservation reservation, AssistenteProviderCall call, CancellationToken ct) =>
            throw new AssistenteIaException(503, "AI_USAGE_PERSISTENCE_FAILED", "Falha sintética de persistência.", retryable: true);
        public Task DefinirLimiteOrganizacaoAsync(Guid organizationId, decimal monthlyLimitUsd, Guid updatedById, string reason, CancellationToken ct) => inner.DefinirLimiteOrganizacaoAsync(organizationId, monthlyLimitUsd, updatedById, reason, ct);
        public Task<AssistenteReconciliationResult> ReconciliarIndeterminadaAsync(Guid reservationId, bool confirmarConsumo, int? inputTokens, int? cachedInputTokens, int? outputTokens, Guid operatorUserId, string reason, CancellationToken ct, int? cacheWriteTokens = null) => inner.ReconciliarIndeterminadaAsync(reservationId, confirmarConsumo, inputTokens, cachedInputTokens, outputTokens, operatorUserId, reason, ct, cacheWriteTokens);
        public Task<AssistenteReconciliationResult> RecuperarReservaOrfaAsync(Guid reservationId, AssistenteOrphanRecoveryAction action, bool confirmWorkersDrained, string drainEvidence, int? inputTokens, int? cachedInputTokens, int? outputTokens, Guid operatorUserId, string reason, CancellationToken ct, int? cacheWriteTokens = null) => inner.RecuperarReservaOrfaAsync(reservationId, action, confirmWorkersDrained, drainEvidence, inputTokens, cachedInputTokens, outputTokens, operatorUserId, reason, ct, cacheWriteTokens);
    }

    [ReviewPostgresFact]
    public async Task PrecosInvalidos_BloqueiaProvedorRealMesmoEmProducaoComFlagOptOutEMensagemSemDetalhesFinanceiros()
    {
        await using var db = await P03Database.CreateAsync();
        // Missing/zero prices AND even if AllowSimulatedWithoutFinancialControl=true in Production!
        var invalidSettings = new AssistenteIaSettings
        {
            Enabled = true,
            Model = "gpt-test-p03",
            ApiKey = "chave-simulada-p03",
            PricingVersion = "unconfigured",
            DefaultMonthlyLimitUsd = 5.00m,
            InputCostPerMillionTokensUsd = 0m,
            CachedInputCostPerMillionTokensUsd = 0m,
            OutputCostPerMillionTokensUsd = 0m,
            AllowSimulatedWithoutFinancialControl = true
        };
        var providerCalls = 0;
        var handler = new MemoryHandler(_ => { providerCalls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); });

        await using var ctx = db.Context();
        var app = db.AppService(ctx, invalidSettings, handler, environmentName: "Production");
        var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => app.PrepararAsync(db.PecaId,
            new CriarSolicitacaoAssistenteRequest(Guid.NewGuid(), AssistenteModo.Perguntar, "Teste", 1,
                AssistenteOperacao.ResponderNoPainel, new(new(AssistenteEscopoDocumento.Nenhum))), default));

        Assert.Equal("AI_LIMIT_CONFIGURATION_INVALID", ex.Code);
        Assert.Equal(503, ex.StatusCode);
        Assert.DoesNotContain("$", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("USD", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preço", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tarifa", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, providerCalls);
    }

    [ReviewPostgresFact]
    public async Task FalhaDeFormatoDepoisDeUsageConhecido_ContinuaCobrandoConsumo()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 5.00m);
        settings.CacheWriteCostPerMillionTokensUsd = 2.50m;

        var handler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":1000}""") });
            // Invalid JSON text output, BUT response.completed returned valid usage!
            return Task.FromResult(MemoryStream("{not-valid-json", 1_000, 400, 250, cacheWriteTokens: 500));
        });

        await using (var ctx = db.Context())
        {
            var app = db.AppService(ctx, settings, handler);
            var exec = await app.PrepararAsync(db.PecaId, new CriarSolicitacaoAssistenteRequest(
                Guid.NewGuid(), AssistenteModo.Perguntar, "Pergunta", 1, AssistenteOperacao.ResponderNoPainel,
                new(new(AssistenteEscopoDocumento.Nenhum))), default);
            try
            {
                var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => app.GerarAsync(exec, default));
                Assert.Equal("AI_PROVIDER_ERROR", ex.Code);
            }
            finally { await app.FinalizarAsync(exec); }
        }

        await using var verify = db.Context();
        var res = await verify.AiUsageReservations.SingleAsync();
        Assert.Equal("Settled", res.Status);
        // 100 ordinary, 400 cache reads, 500 cache writes and 250 output tokens.
        Assert.Equal(0.00365m, res.SettledUsd);
        Assert.Equal(500, res.ActualCacheWriteTokens);
        var account = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.Equal(0.00365m, account.ConsumedUsd);
        Assert.Equal(0m, account.ReservedUsd);
        var ledger = await verify.AiUsageLedger.SingleAsync();
        Assert.Equal(0.00365m, ledger.EstimatedCostUsd);
        Assert.Equal(500, ledger.CacheWriteTokens);
        Assert.Equal("failed", ledger.Status);
    }

    [ReviewPostgresFact]
    public async Task MultiplasChamadasFisicasPorFerramentas_ContabilizaCadaEtapaSemDuplicarLedgerOuGasto()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 5.00m);
        settings.CacheWriteCostPerMillionTokensUsd = 2.50m;
        var turn = 0;

        var handler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":500}""") });
            turn++;
            if (turn == 1)
            {
                var callJson = JsonSerializer.Serialize(new { type = "function_call", call_id = "call-1", name = "listar_fontes", arguments = """{"aposIndice":0,"limite":5,"coberturaPretendida":"Relevantes"}""" });
                return Task.FromResult(MemoryStreamTool(callJson, 500, 0, 100, cacheWriteTokens: 500));
            }
            var finalJson = """{"titulo":"Ok","texto":"Concluído.","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false,"coberturaPretendida":"Relevantes"}""";
            return Task.FromResult(MemoryStream(finalJson, 700, 200, 150, cacheWriteTokens: 300));
        });

        await using (var ctx = db.Context())
        {
            var docSettings = Options.Create(new AssistenteDocumentSettings { Enabled = true, RetrievalEnabled = true });
            var repo = new AssistenteWorkspaceRepository(ctx);
            var pieces = new PecaRepository(ctx);
            var auth = new TestAuthContext(db.OrgId, db.UserId);
            var limitSvc = db.LimitService(ctx, settings);
            var docs = new AssistenteDocumentService(ctx, repo, pieces, new InMemoryStorage(), auth, db.Access, new Transparency(), docSettings);
            var chat = new AssistenteChatService(repo, pieces, auth, db.Access, new Transparency(), limitSvc, new Monitor(settings), docs);
            var app = db.AppService(ctx, settings, handler, docs: docs);

            var conv = await new AssistenteWorkspaceService(repo, pieces, auth, db.Access, new Transparency(), new Monitor(settings), new AssistentePatchCompiler(), docs)
                .ObterOuCriarAsync(db.PecaId, default);
            var req = new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Consulte os anexos e responda.", 1).Solicitar();
            var start = await chat.IniciarAsync(conv.Id, req, default);
            var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask);
            var exec = await app.PrepararAsync(db.PecaId, req, default, start.Contexto, conversaLivre: true, tools);
            try
            {
                var result = await app.GerarAsync(exec, default);
                Assert.Equal("Concluído.", result.Sugestao.Texto);
            }
            finally { await app.FinalizarAsync(exec); }
        }

        await using var verify = db.Context();
        var reservations = await verify.AiUsageReservations.OrderBy(r => r.AttemptCount).ToArrayAsync();
        Assert.Equal(2, reservations.Length);
        Assert.Equal(new[] { 1, 2 }, reservations.Select(r => r.AttemptCount).ToArray());

        Assert.Equal(new int?[] { 500, 300 }, reservations.Select(r => r.ActualCacheWriteTokens));
        // Turn 1 writes 500 tokens. Turn 2 mixes 200 ordinary, 200 read and 300 written.
        var ledger = await verify.AiUsageLedger.OrderBy(l => l.AttemptCount).ToArrayAsync();
        Assert.Equal(2, ledger.Length);
        Assert.Equal(0.00205m, ledger[0].EstimatedCostUsd);
        Assert.Equal(0.00245m, ledger[1].EstimatedCostUsd);

        var account = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.Equal(0.00450m, account.ConsumedUsd);
        Assert.Equal(0m, account.ReservedUsd);
    }

    [ReviewPostgresFact]
    public async Task GravacaoCache_ReservaTarifaMaiorEReconciliacaoUsaTarifaCongelada()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 0.01025m);
        settings.CacheWriteCostPerMillionTokensUsd = 2.50m;
        await using var context = db.Context();
        var service = db.LimitService(context, settings);
        var request = new AssistenteFinancialReserveRequest(
            db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "CacheWrite", 1_000, 1_000);

        var blocked = await Assert.ThrowsAsync<AssistenteIaException>(() => service.ReservarAsync(request, default));
        Assert.Equal("AI_MONTHLY_LIMIT_INSUFFICIENT", blocked.Code);

        await service.DefinirLimiteOrganizacaoAsync(db.OrgId, 0.011m, db.UserId, "Teste de tarifa de gravação", default);
        var reservation = await service.ReservarAsync(request, default);
        Assert.Equal(0.0105m, reservation.ReservedUsd);
        settings.CacheWriteCostPerMillionTokensUsd = 9m;

        var pending = await service.LiquidarAsync(reservation, 1_000, 200, 500, null, default);
        Assert.Equal("Indeterminate", pending.Status);
        Assert.Null(pending.SettledUsd);
        var stillReserved = await context.AiMonthlyAccounts.AsNoTracking().SingleAsync();
        Assert.Equal(0.0105m, stillReserved.ReservedUsd);
        Assert.Equal(0m, stillReserved.ConsumedUsd);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReconciliarIndeterminadaAsync(
            reservation.ReservationId, true, 1_000, 200, 500, db.UserId, "Uso incompleto", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReconciliarIndeterminadaAsync(
            reservation.ReservationId, true, 1_000, 200, 500, db.UserId, "Contagem impossível", default, cacheWriteTokens: 900));

        var reconciled = await service.ReconciliarIndeterminadaAsync(
            reservation.ReservationId, true, 1_000, 200, 500, db.UserId, "Uso confirmado", default, cacheWriteTokens: 300);
        Assert.Equal(0.00585m, reconciled.SettledUsd);
        var row = await context.AiUsageReservations.AsNoTracking().SingleAsync();
        Assert.Equal(2.50m, row.CacheWriteCostPerMillionUsd);
        Assert.Equal(300, row.ActualCacheWriteTokens);
        var account = await context.AiMonthlyAccounts.AsNoTracking().SingleAsync();
        Assert.Equal(0m, account.ReservedUsd);
        Assert.Equal(0.00585m, account.ConsumedUsd);
    }

    [ReviewPostgresFact]
    public async Task OverridePersistidoEPercentuaisEndpoint_SemValoresMonetariosEDaOrganizacaoDoAuth()
    {
        await using var db = await P03Database.CreateAsync();
        var otherOrg = await db.CreateSecondOrganizationAsync();
        var settings = PricedSettings(monthlyLimitUsd: 5.00m);

        await using (var ctx = db.Context())
        {
            var svc = db.LimitService(ctx, settings);
            await svc.DefinirLimiteOrganizacaoAsync(db.OrgId, 0.01m, db.UserId, "Ajuste piloto", default);
            var r = await svc.ReservarAsync(new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Op", 1_000, 1_000), default);
            // Settle 70% of 0.01 USD = 0.007 USD (1000 input uncached = 0.002 + 625 output = 0.005 => 0.007)
            await svc.LiquidarAsync(r, 1_000, 0, 625, null, default);
            // Reserve another 20% = 0.002 USD (1000 input uncached = 0.002, 0 output)
            await svc.ReservarAsync(new(db.OrgId, db.UserId, Guid.NewGuid(), 2, Guid.NewGuid(), Guid.NewGuid(), "Op2", 1_000, 0), default);
        }

        await using var api = await P03HttpTestApi.CreateAsync(db.ConnectionString, settings);
        using var anonymous = api.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("assistente/limite-mensal")).StatusCode);

        // Even if query string tries to pass another organizationId, endpoint uses authContext.OrganizationId only!
        using var client = api.CreateClient(db.OrgId, db.UserId);
        var response = await client.GetAsync($"assistente/limite-mensal?organizationId={otherOrg:D}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rawJson = await response.Content.ReadAsStringAsync();

        // Zero monetary / dollar / budget terms in public DTO JSON
        Assert.DoesNotContain("usd", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dolar", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cost", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("price", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orcamento", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("budget", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$", rawJson, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<AssistenteLimiteMensalDto>(rawJson, P03HttpTestApi.JsonOptions)!;
        Assert.Equal(AssistenteLimiteMensalEstado.Critico90, dto.Estado);
        Assert.Equal(70m, dto.PercentualUtilizado);
        Assert.Equal(20m, dto.PercentualReservado);
        Assert.Equal(10m, dto.PercentualDisponivel);
        Assert.True(dto.PossuiReservasEmAndamento);
        Assert.False(dto.BloqueadoParaNovasChamadas);
    }

    [ReviewPostgresFact]
    public async Task WorkersDuraveis_GeracaoEAnaliseContabilizamCadaChamadaFisicaEInterrompemAoAtingirLimite()
    {
        await using var db = await P03Database.CreateAsync();
        // Limit = 0.0055 USD. Each batch/section with 500 input + 1000 maxOutput = 0.009 USD if maxOutput=1000.
        // Set MaxOutputTokens = 200: conservative reserve = (500*2 + 200*8)/1e6 = 0.0026 USD.
        // Call 1 (reading batch): 500 in + 100 out -> settled = 0.0018 USD. Remaining = 0.0037 USD.
        // Call 2 (final answer): 500 in + 100 out -> settled = 0.0018 USD. Total = 0.0036 USD.
        var settings = PricedSettings(monthlyLimitUsd: 0.0055m);
        settings.MaxOutputTokens = 200;
        var docConfig = new AssistenteDocumentSettings
        {
            Enabled = true, RetrievalEnabled = true, AnalysisEnabled = true,
            AnalysisOutputTokens = 200, AnalysisSummaryCharacters = 2000
        };

        await using (var seed = db.Context())
        {
            seed.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(), UserId = db.UserId, OrganizationId = db.OrgId,
                Role = MembershipRoleEnum.Owner, DataCriacao = DateTime.UtcNow, DataAtualizacao = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var docStorage = new InMemoryStorage();
        var handler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":500}""") });
            using var json = JsonDocument.Parse(req.Content!.ReadAsStringAsync().Result);
            var body = json.RootElement;
            if (body.TryGetProperty("tool_choice", out var choice) && choice.ValueKind == JsonValueKind.Object &&
                choice.GetProperty("name").GetString() == "registrar_sintese_documental")
            {
                using var inp = JsonDocument.Parse(body.GetProperty("input")[0].GetProperty("content").GetString()!);
                var chunk = inp.RootElement.GetProperty("entradas").GetProperty("conteudos")[0];
                var id = chunk.GetProperty("trechoId").GetGuid();
                var quote = chunk.GetProperty("texto").GetString()![..Math.Min(40, chunk.GetProperty("texto").GetString()!.Length)];
                var args = JsonSerializer.Serialize(new { registros = new[] { new { categoria = "Valor", texto = "Valor apurado.", origens = new[] { new { trechoId = id, citacao = quote } } } } });
                var callJson = JsonSerializer.Serialize(new { type = "function_call", call_id = Guid.NewGuid().ToString(), name = "registrar_sintese_documental", arguments = args });
                return Task.FromResult(MemoryStreamTool(callJson, 500, 0, 100));
            }
            return Task.FromResult(MemoryStream("""{"titulo":"Síntese","texto":"Análise concluída.","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false,"coberturaPretendida":"Todos"}""", 500, 0, 100));
        });

        Guid analysisId;
        await using (var ctx = db.Context())
        {
            var repo = new AssistenteWorkspaceRepository(ctx);
            var pieces = new PecaRepository(ctx);
            var auth = new TestAuthContext(db.OrgId, db.UserId);
            var limitSvc = db.LimitService(ctx, settings);
            var docs = new AssistenteDocumentService(ctx, repo, pieces, docStorage, auth, db.Access, new Transparency(), Options.Create(docConfig));
            var analyses = new AssistenteDocumentAnalysisService(ctx, repo, pieces, auth, db.Access, new Transparency(), limitSvc, docs, Options.Create(docConfig), new Monitor(settings), new AssistenteBackgroundIdentity());
            var chat = new AssistenteChatService(repo, pieces, auth, db.Access, new Transparency(), limitSvc, new Monitor(settings), docs, analysis: analyses);

            var conv = await new AssistenteWorkspaceService(repo, pieces, auth, db.Access, new Transparency(), new Monitor(settings), new AssistentePatchCompiler(), docs)
                .ObterOuCriarAsync(db.PecaId, default);
            var bytes = Encoding.UTF8.GetBytes("Cláusula primeira: valor de R$ 1.200,00 confirmado pelas partes.");
            var doc = await docs.UploadAsync(conv.Id, Guid.NewGuid(), "contrato.txt", new MemoryStream(bytes), default);
            await using (var extractCtx = db.Context())
            {
                var docProc = new AssistenteDocumentProcessor(extractCtx, docStorage, new LocalDocumentExtractor(new DocumentProcess(), Options.Create(docConfig)), Options.Create(docConfig));
                Assert.True(await docProc.ProcessNextAsync(default));
            }

            var start = await chat.IniciarAsync(conv.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Analise todos os documentos.", 1, AnexoIds: [doc.Id]).Solicitar(), default);
            var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
            var queued = await Assert.ThrowsAsync<AssistenteAnalysisQueuedException>(() =>
                tools.ExecuteAsync("analisar_fontes", """{"coberturaPretendida":"Todos"}""", default));
            analysisId = queued.Analysis.Id;
        }

        for (var i = 0; i < 10; i++)
        {
            await using var stepCtx = db.Context();
            var state = (await stepCtx.Set<AssistenteAnaliseDocumental>().AsNoTracking().SingleAsync(a => a.Id == analysisId)).Estado;
            if (!AssistenteDocumentAnalysisService.Active(state)) break;
            var proc = db.AnalysisProcessor(stepCtx, settings, docConfig, docStorage, handler);
            Assert.True(await proc.ProcessNextAsync(default));
        }

        await using var verify = db.Context();
        var done = await verify.Set<AssistenteAnaliseDocumental>().AsNoTracking().SingleAsync(a => a.Id == analysisId);
        Assert.Equal("Concluida", done.Estado);
        Assert.Equal(2, await verify.AiUsageReservations.CountAsync());
        Assert.Equal(2, await verify.AiUsageLedger.CountAsync());
        var account = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.Equal(0.0036m, account.ConsumedUsd);
        Assert.Equal(0m, account.ReservedUsd);
    }

    [ReviewPostgresFact]
    public async Task WorkerGeracaoDuravel_ContabilizaSecoesEInterrompePreservandoCheckpointsAoEsgotarLimite()
    {
        await using var db = await P03Database.CreateAsync();
        // Conservative reserve per call (input=500 * 2 + maxOut=200 * 8)/1e6 = 0.0026 USD.
        // Actual settled per call (input=500 * 2 + out=100 * 8)/1e6 = 0.0018 USD.
        // With limit = 0.0044 USD:
        // Call 1 (plan): reserves 0.0026 (avail 0.0018), settles 0.0018 (avail 0.0026).
        // Call 2 (section 1): reserves 0.0026 (avail 0.0000), settles 0.0018 -> total consumed 0.0036, avail 0.0008.
        // Call 3 (section 2): needs 0.0026 > 0.0008 -> blocked BEFORE calling provider, job Interrompida with AI_MONTHLY_LIMIT_INSUFFICIENT, section 1 preserved!
        var settings = PricedSettings(monthlyLimitUsd: 0.0044m);
        settings.MaxOutputTokens = 200;
        var docConfig = new AssistenteDocumentSettings { Enabled = true, RetrievalEnabled = true };

        await using (var seed = db.Context())
        {
            seed.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(), UserId = db.UserId, OrganizationId = db.OrgId,
                Role = MembershipRoleEnum.Owner, DataCriacao = DateTime.UtcNow, DataAtualizacao = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var calls = 0;
        var handler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":500}""") });
            calls++;
            using var json = JsonDocument.Parse(req.Content!.ReadAsStringAsync().Result);
            var body = json.RootElement;
            if (body.GetProperty("tools").EnumerateArray().Any(t => t.GetProperty("name").GetString() == "registrar_secao"))
            {
                var text = body.GetProperty("input")[0].GetProperty("content").GetString()!;
                var marker = "[PLANO_E_SECAO dado_nao_confiavel=\"true\"]\n";
                using var details = JsonDocument.Parse(text[(text.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..text.LastIndexOf("\n[/PLANO_E_SECAO]", StringComparison.Ordinal)]);
                var id = details.RootElement.GetProperty("secaoAtual").GetProperty("id").GetString()!;
                var args = JsonSerializer.Serialize(new
                {
                    secaoId = id, concluida = true, sumario = "Sumário " + id,
                    blocos = new[] { AssistentePatchCompilerTests.Paragraph("Conteúdo da " + id) }
                });
                var callJson = JsonSerializer.Serialize(new { type = "function_call", call_id = Guid.NewGuid().ToString(), name = "registrar_secao", arguments = args });
                return Task.FromResult(MemoryStreamTool(callJson, 500, 0, 100));
            }
            var planArgs = JsonSerializer.Serialize(new
            {
                titulo = "Minuta", substituirPropostaReferenciada = false,
                secoes = new[]
                {
                    new { titulo = "Fatos", objetivo = "Narrar fatos" },
                    new { titulo = "Direito", objetivo = "Fundamentar" }
                }
            });
            var planCall = JsonSerializer.Serialize(new { type = "function_call", call_id = Guid.NewGuid().ToString(), name = "planejar_minuta", arguments = planArgs });
            return Task.FromResult(MemoryStreamTool(planCall, 500, 0, 100));
        });

        Guid generationId;
        await using (var ctx = db.Context())
        {
            var repo = new AssistenteWorkspaceRepository(ctx);
            var pieces = new PecaRepository(ctx);
            var cases = new CasoRepository(ctx);
            var auth = new TestAuthContext(db.OrgId, db.UserId);
            var limitSvc = db.LimitService(ctx, settings);
            var docs = new AssistenteDocumentService(ctx, repo, pieces, new InMemoryStorage(), auth, db.Access, new Transparency(), Options.Create(docConfig));
            var generations = new AssistenteGenerationService(ctx, repo, pieces, cases, auth, db.Access, new Transparency(), limitSvc, docs, new Monitor(settings), new AssistenteBackgroundIdentity(), new CorrelationStub());
            var chat = new AssistenteChatService(repo, pieces, auth, db.Access, new Transparency(), limitSvc, new Monitor(settings), docs, generations: generations);

            await new AssistenteWorkspaceService(repo, pieces, auth, db.Access, new Transparency(), new Monitor(settings), new AssistentePatchCompiler(), docs)
                .ObterOuCriarAsync(db.PecaId, default);
            var conv = await ctx.AssistenteConversas.SingleAsync();
            var start = await chat.IniciarAsync(conv.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Elabore a minuta completa.", 1).Solicitar(), default);
            generationId = start.Resposta.GeracaoId!.Value;
        }

        for (var i = 0; i < 6; i++)
        {
            await using var stepCtx = db.Context();
            var state = (await stepCtx.Set<AssistenteGeracao>().AsNoTracking().SingleAsync(g => g.Id == generationId)).Estado;
            if (!AssistenteGenerationService.Active(state)) break;
            var proc = db.GenerationProcessor(stepCtx, settings, docConfig, new InMemoryStorage(), handler);
            Assert.True(await proc.ProcessNextAsync(default));
        }

        await using var verify = db.Context();
        var job = await verify.Set<AssistenteGeracao>().AsNoTracking().SingleAsync(g => g.Id == generationId);
        Assert.Equal("Interrompida", job.Estado);
        Assert.Equal("AI_MONTHLY_LIMIT_INSUFFICIENT", job.CodigoErro);
        Assert.Equal(2, calls);
        Assert.Single(await verify.Set<AssistenteGeracaoEtapa>().Where(s => s.GeracaoId == generationId && s.Tipo == "Secao:secao-1" && s.Estado == "Confirmada").ToArrayAsync());
        Assert.Equal(2, await verify.AiUsageReservations.CountAsync());
        Assert.Equal(2, await verify.AiUsageLedger.CountAsync());
        var account = await verify.AiMonthlyAccounts.SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.Equal(0.0036m, account.ConsumedUsd);
        Assert.Equal(0m, account.ReservedUsd);
    }

    [ReviewPostgresFact]
    public async Task SseAntesEDuranteStream_RetornaErroSemDetalhesFinanceiros()
    {
        await using var db = await P03Database.CreateAsync();
        // Limit = 0.005 USD -> insufficient for 1000 in + 1000 maxOut (0.010 USD)
        var settings = PricedSettings(monthlyLimitUsd: 0.005m);
        await using var api = await P03HttpTestApi.CreateAsync(db.ConnectionString, settings,
            req => req.RequestUri!.AbsolutePath.EndsWith("input_tokens")
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":1000}""") })
                : Task.FromResult(MemoryStream("""{"titulo":"R","texto":"T","resumo":"","operacaoSugerida":"ResponderNoPainel","alertas":[],"achados":[]}""", 1000, 0, 100)));

        using var client = api.CreateClient(db.OrgId, db.UserId);
        var body = JsonContent.Create(new CriarSolicitacaoAssistenteRequest(
            Guid.NewGuid(), AssistenteModo.Perguntar, "Pergunta", 1, AssistenteOperacao.ResponderNoPainel,
            new(new(AssistenteEscopoDocumento.Nenhum))));
        var response = await client.PostAsync($"pecas/{db.PecaId:D}/assistente/solicitacoes", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("AI_MONTHLY_LIMIT_INSUFFICIENT", payload);
        Assert.DoesNotContain("$", payload);
        Assert.DoesNotContain("USD", payload, StringComparison.OrdinalIgnoreCase);
    }

    [ReviewPostgresFact]
    public async Task CorridaLimiteAdministrativo_ReducaoConcorrenteComBarreiraDeterministicaImpedeLostUpdate()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 5.00m);
        settings.InputCostPerMillionTokensUsd = 1_000_000m;
        settings.CachedInputCostPerMillionTokensUsd = 1_000_000m;
        settings.OutputCostPerMillionTokensUsd = 1_000_000m;

        await using (var seed = db.Context())
        {
            var svc = db.LimitService(seed, settings);
            await svc.DefinirLimiteOrganizacaoAsync(db.OrgId, 5m, db.UserId, "Limite inicial", default);
        }

        var barrier = new AccountLockBarrier();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await using var reservingCtx = db.Context(barrier);
        var pending = db.LimitService(reservingCtx, settings).ReservarAsync(
            new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Review", 4, 0),
            timeout.Token);

        await barrier.Entered.Task.WaitAsync(timeout.Token);

        await using (var adminCtx = db.Context())
        {
            await db.LimitService(adminCtx, settings).DefinirLimiteOrganizacaoAsync(db.OrgId, 1m, db.UserId, "Redução concorrente", timeout.Token);
        }

        barrier.Release.TrySetResult();

        var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => pending);
        Assert.Equal("AI_MONTHLY_LIMIT_INSUFFICIENT", ex.Code);

        await using var verify = db.Context();
        var orgOverride = await verify.AiOrganizationMonthlyLimits.AsNoTracking().SingleAsync();
        var account = await verify.AiMonthlyAccounts.AsNoTracking().SingleAsync();
        Assert.Equal(1m, orgOverride.MonthlyLimitUsd);
        Assert.Equal(1m, account.LimitUsd);
        Assert.Equal(0m, account.ReservedUsd);
    }

    [ReviewPostgresFact]
    public async Task ReservaOrfaReserved_ExigeConfirmacaoEDrenagemESuportaRecuperacaoAuditadaEIdempotente()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 5.00m);
        Guid orphan1Id;
        Guid orphan2Id;

        await using (var ctx = db.Context())
        {
            var svc = db.LimitService(ctx, settings);
            var r1 = await svc.ReservarAsync(new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Orphan1", 1_000, 1_000), default);
            var r2 = await svc.ReservarAsync(new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Orphan2", 1_000, 1_000), default);
            orphan1Id = r1.ReservationId;
            orphan2Id = r2.ReservationId;
        }

        // Simulate restart with fresh DbContext: both are still Status=Reserved
        await using (var afterRestart = db.Context())
        {
            var svc = db.LimitService(afterRestart, settings);

            // ReconciliarIndeterminadaAsync refuses Reserved
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ReconciliarIndeterminadaAsync(orphan1Id, false, null, null, null, db.UserId, "Sem drenagem", default));

            // RecuperarReservaOrfaAsync rejects missing confirmation or drain evidence
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RecuperarReservaOrfaAsync(orphan1Id, AssistenteOrphanRecoveryAction.Release, false, "workers parados", null, null, null, db.UserId, "Motivo", default));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RecuperarReservaOrfaAsync(orphan1Id, AssistenteOrphanRecoveryAction.Release, true, "   ", null, null, null, db.UserId, "Motivo", default));

            // Conservative transition Reserved -> Indeterminate keeps reserved_usd
            var marked = await svc.RecuperarReservaOrfaAsync(
                orphan1Id, AssistenteOrphanRecoveryAction.MarkIndeterminate, true, "systemd stop workers @ 18:00Z",
                null, null, null, db.UserId, "Processo caiu antes de registrar retorno", default);
            Assert.Equal("Indeterminate", marked.Status);

            var accMid = await afterRestart.AiMonthlyAccounts.AsNoTracking().SingleAsync(a => a.OrganizationId == db.OrgId);
            Assert.Equal(0.02m, accMid.ReservedUsd);

            // Settle orphan1 conservatively without token usage
            var settled = await svc.RecuperarReservaOrfaAsync(
                orphan1Id, AssistenteOrphanRecoveryAction.Settle, true, "systemd stop workers @ 18:00Z",
                null, null, null, db.UserId, "Confirmado consumo conservador", default);
            Assert.Equal("Settled", settled.Status);
            Assert.Equal(0.01m, settled.SettledUsd);

            // Direct release of orphan2 with drain evidence, plus idempotency
            var released1 = await svc.RecuperarReservaOrfaAsync(
                orphan2Id, AssistenteOrphanRecoveryAction.Release, true, "workers drenados e fila vazia",
                null, null, null, db.UserId, "Sem faturamento no provedor", default);
            var released2 = await svc.RecuperarReservaOrfaAsync(
                orphan2Id, AssistenteOrphanRecoveryAction.Release, true, "workers drenados e fila vazia",
                null, null, null, db.UserId, "Reexecução idempotente", default);
            Assert.Equal("Released", released1.Status);
            Assert.Equal("Released", released2.Status);
        }

        await using var verify = db.Context();
        var account = await verify.AiMonthlyAccounts.AsNoTracking().SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.Equal(0.01m, account.ConsumedUsd);
        Assert.Equal(0m, account.ReservedUsd);
        Assert.Equal(3, await verify.AiFinancialAuditLogs.CountAsync());
    }

    [Fact]
    public async Task UsageRecorder_FrozenPricingVersionPreservaCustoDesconhecidoSemRecalcularComTarifaAtual()
    {
        var repo = new CapturingUsageRepository();
        var settings = PricedSettings();
        var recorder = new AssistenteUsageRecorder(repo, new Monitor(settings), NullLogger<AssistenteUsageRecorder>.Instance);

        // Indeterminate with frozen PricingVersion and FrozenEstimatedCostUsd = null MUST stay null!
        await recorder.RegistrarAsync(new AssistenteUsageRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            AssistenteModo.Perguntar, null, "OpenAI", "gpt-test-p03", "v1", 1,
            InputTokens: 500, CachedInputTokens: null, OutputTokens: null,
            Status: "failed", ErrorCode: "AI_PROVIDER_TIMEOUT", DurationMs: 100, RequirePersistence: true,
            FrozenEstimatedCostUsd: null, FrozenPricingVersion: "frozen-v1"), default);

        var indeterminate = Assert.Single(repo.Entries);
        Assert.Null(indeterminate.EstimatedCostUsd);
        Assert.Equal("frozen-v1", indeterminate.PricingVersion);

        // Mutate settings after reservation; settled entry keeps frozen cost & version
        settings.InputCostPerMillionTokensUsd = 999m;
        settings.PricingVersion = "new-v2";
        await recorder.RegistrarAsync(new AssistenteUsageRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            AssistenteModo.Perguntar, null, "OpenAI", "gpt-test-p03", "v1", 2,
            InputTokens: 500, CachedInputTokens: 100, OutputTokens: 50,
            Status: "succeeded", ErrorCode: null, DurationMs: 100, RequirePersistence: true,
            FrozenEstimatedCostUsd: 0.00125m, FrozenPricingVersion: "frozen-v1"), default);

        Assert.Equal(0.00125m, repo.Entries[1].EstimatedCostUsd);
        Assert.Equal("frozen-v1", repo.Entries[1].PricingVersion);

        // Without FrozenPricingVersion (legacy non-snapshot), fallback still calculates from current settings
        await recorder.RegistrarAsync(new AssistenteUsageRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            AssistenteModo.Perguntar, null, "OpenAI", "gpt-test-p03", "v1", 3,
            InputTokens: 1_000, CachedInputTokens: 0, OutputTokens: 0,
            Status: "succeeded", ErrorCode: null, DurationMs: 100), default);

        Assert.Equal(0.999m, repo.Entries[2].EstimatedCostUsd);
        Assert.Equal("new-v2", repo.Entries[2].PricingVersion);
    }

    [ReviewPostgresFact]
    public async Task Ferramentas_LimitedAsyncSemChamadaNaoCriaReservaNemLedger_EFalhaDeLedgerNoTurno2PreservaErroSemDuplicar()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 5.00m);

        // 1) LimitedAsync before round 0 (MaxContextCharacters small): 0 reservations, 0 ledger rows!
        await using (var ctx = db.Context())
        {
            var providerCalls = 0;
            var handler = new MemoryHandler(req =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":50}""") });
                providerCalls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
            var app = db.AppService(ctx, settings, handler);
            var tools = new AssistenteDocumentTools(
                JsonDocument.Parse("[]").RootElement,
                (_, _, _) => Task.FromResult(""),
                () => new("Completa", "Relevantes", []),
                maxRounds: 2, maxTotalTokens: 10_000, maxContextCharacters: 5); // smaller than input!
            var exec = await app.PrepararAsync(db.PecaId, new CriarSolicitacaoAssistenteRequest(
                Guid.NewGuid(), AssistenteModo.Perguntar, "Pergunta maior que 5 chars", 1,
                AssistenteOperacao.ResponderNoPainel, new(new(AssistenteEscopoDocumento.Nenhum)), InteracaoAutomatica: true),
                default, "ctx", conversaLivre: true, ferramentasDocumentais: tools);
            try
            {
                var limitedResult = await app.GerarAsync(exec, default);
                Assert.Equal("Consulta incompleta", limitedResult.Sugestao.Titulo);
            }
            finally { await app.FinalizarAsync(exec); }

            Assert.Equal(0, providerCalls);
            Assert.Empty(await ctx.AiUsageReservations.ToArrayAsync());
            Assert.Empty(await ctx.AiUsageLedger.ToArrayAsync());
        }

        // 2) Two turns where turn 2 ledger insert fails AFTER turn 2 financial settlement:
        //    preserves AI_USAGE_PERSISTENCE_FAILED and never writes duplicate attempt=1 aggregate row!
        var turn = 0;
        var turnHandler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":500}""") });
            turn++;
            if (turn == 1)
            {
                var callJson = JsonSerializer.Serialize(new { type = "function_call", call_id = "c1", name = "listar_fontes", arguments = """{"aposIndice":0,"limite":5,"coberturaPretendida":"Relevantes"}""" });
                return Task.FromResult(MemoryStreamTool(callJson, 500, 0, 100));
            }
            return Task.FromResult(MemoryStream("""{"titulo":"Ok","texto":"Fim","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false,"coberturaPretendida":"Relevantes"}""", 500, 0, 100));
        });

        await using (var ctx2 = db.Context())
        {
            var docSettings = Options.Create(new AssistenteDocumentSettings { Enabled = true, RetrievalEnabled = true });
            var repo = new AssistenteWorkspaceRepository(ctx2);
            var pieces = new PecaRepository(ctx2);
            var auth = new TestAuthContext(db.OrgId, db.UserId);
            var limitSvc = db.LimitService(ctx2, settings);
            var docs = new AssistenteDocumentService(ctx2, repo, pieces, new InMemoryStorage(), auth, db.Access, new Transparency(), docSettings);
            var chat = new AssistenteChatService(repo, pieces, auth, db.Access, new Transparency(), limitSvc, new Monitor(settings), docs);
            var failingSecondLedger = new FailOnSecondLedgerRepository(ctx2);
            var provider = new OpenAiAssistenteIaProvider(new HttpClient(turnHandler) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(settings));
            var monitor = new Monitor(settings);
            var usage = new AssistenteUsageRecorder(failingSecondLedger, monitor, NullLogger<AssistenteUsageRecorder>.Instance);
            var app = new AssistenteIaApplicationService(pieces, new CasoRepository(ctx2), auth, provider, provider,
                new AssistenteContextBuilder(Options.Create(settings)), new AssistentePromptPolicy(), new AssistenteRequestRegistry(),
                db.Access, new Transparency(), new AssistenteUsageLimiter(monitor), usage, limitSvc, new CorrelationStub(),
                monitor, NullLogger<AssistenteIaApplicationService>.Instance, docSettings);

            var conv = await new AssistenteWorkspaceService(repo, pieces, auth, db.Access, new Transparency(), monitor, new AssistentePatchCompiler(), docs)
                .ObterOuCriarAsync(db.PecaId, default);
            var req = new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Consulte e responda.", 1).Solicitar();
            var start = await chat.IniciarAsync(conv.Id, req, default);
            var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask);
            var exec = await app.PrepararAsync(db.PecaId, req, default, start.Contexto, conversaLivre: true, tools);
            try
            {
                var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => app.GerarAsync(exec, default));
                Assert.Equal("AI_USAGE_PERSISTENCE_FAILED", ex.Code);
            }
            finally { await app.FinalizarAsync(exec); }
        }

        await using var verify2 = db.Context();
        // Turn 1 ledger exists once; turn 2 never fell back to attempt=1 aggregate!
        var ledgerRows = await verify2.AiUsageLedger.ToArrayAsync();
        Assert.Single(ledgerRows);
        Assert.Equal(1, ledgerRows[0].AttemptCount);
        // Both turns settled financially in reservations
        var resRows = await verify2.AiUsageReservations.OrderBy(r => r.AttemptCount).ToArrayAsync();
        Assert.Equal(2, resRows.Length);
        Assert.All(resRows, r => Assert.Equal("Settled", r.Status));
    }

    [ReviewPostgresFact]
    public async Task ProviderJaCriadoComModeloAntigo_BloqueiaQuandoMonitorMudaModeloAntesDaChamada()
    {
        await using var db = await P03Database.CreateAsync();
        var providerFrozenSettings = PricedSettings(monthlyLimitUsd: 5.00m);
        providerFrozenSettings.Model = "gpt-modelo-antigo";
        var monitorSettings = PricedSettings(monthlyLimitUsd: 5.00m);
        monitorSettings.Model = "gpt-modelo-antigo";

        var httpCalls = 0;
        var handler = new MemoryHandler(req =>
        {
            httpCalls++;
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":500}""") });
            return Task.FromResult(MemoryStream("""{"titulo":"R","texto":"T","resumo":"","operacaoSugerida":"ResponderNoPainel","alertas":[],"achados":[]}""", 500, 0, 100));
        });

        // Provider created with frozen IOptions<AssistenteIaSettings> (old model)
        var provider = new OpenAiAssistenteIaProvider(
            new HttpClient(handler) { BaseAddress = new("https://synthetic.test/v1/") },
            Options.Create(providerFrozenSettings));

        await using var ctx = db.Context();
        var monitor = new Monitor(monitorSettings);
        var limitSvc = new AssistenteMonthlyLimitService(ctx, monitor, new TestHostEnvironment("Testing"),
            NullLogger<AssistenteMonthlyLimitService>.Instance);
        var auth = new TestAuthContext(db.OrgId, db.UserId);
        var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(ctx), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
        var app = new AssistenteIaApplicationService(
            new PecaRepository(ctx), new CasoRepository(ctx), auth, provider, provider,
            new AssistenteContextBuilder(Options.Create(monitorSettings)), new AssistentePromptPolicy(),
            new AssistenteRequestRegistry(), db.Access, new Transparency(),
            new AssistenteUsageLimiter(monitor), usage, limitSvc, new CorrelationStub(),
            monitor, NullLogger<AssistenteIaApplicationService>.Instance);

        // First: reload monitor BEFORE PrepararAsync -> blocked before any HTTP call!
        monitorSettings.Model = "gpt-modelo-novo";
        var ex1 = await Assert.ThrowsAsync<AssistenteIaException>(() => app.PrepararAsync(
            db.PecaId,
            new CriarSolicitacaoAssistenteRequest(Guid.NewGuid(), AssistenteModo.Perguntar, "Oi", 1,
                AssistenteOperacao.ResponderNoPainel, new(new(AssistenteEscopoDocumento.Nenhum))),
            default));
        Assert.Equal("AI_CONFIGURATION_DIVERGED", ex1.Code);
        Assert.Equal(503, ex1.StatusCode);
        Assert.Equal(0, httpCalls);

        // Second: PrepararAsync succeeds while aligned, then monitor mutates BEFORE GerarAsync -> reservation released and provider never called!
        monitorSettings.Model = "gpt-modelo-antigo";
        var exec = await app.PrepararAsync(
            db.PecaId,
            new CriarSolicitacaoAssistenteRequest(Guid.NewGuid(), AssistenteModo.Perguntar, "Oi", 1,
                AssistenteOperacao.ResponderNoPainel, new(new(AssistenteEscopoDocumento.Nenhum))),
            default);
        Assert.Equal(1, httpCalls); // only input_tokens

        monitorSettings.Model = "gpt-modelo-novo";
        try
        {
            var ex2 = await Assert.ThrowsAsync<AssistenteIaException>(() => app.GerarAsync(exec, default));
            Assert.Equal("AI_CONFIGURATION_DIVERGED", ex2.Code);
        }
        finally { await app.FinalizarAsync(exec); }

        Assert.Equal(1, httpCalls); // POST responses was NEVER called!
        var res = await ctx.AiUsageReservations.AsNoTracking().SingleAsync();
        Assert.Equal("Released", res.Status);
        var account = await ctx.AiMonthlyAccounts.AsNoTracking().SingleAsync(a => a.OrganizationId == db.OrgId);
        Assert.Equal(0m, account.ReservedUsd);
        Assert.Equal(0m, account.ConsumedUsd);
    }

    [ReviewPostgresFact]
    public async Task SaldoEsgotadoEntrePrepararEGerar_RejeitaSemChamadaOuLedgerFicticio()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 0.015m);
        var providerCalls = 0;
        var handler = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":1000}""") });
            providerCalls++;
            return Task.FromResult(MemoryStream("""{"titulo":"R","texto":"T","resumo":"","operacaoSugerida":"ResponderNoPainel","alertas":[],"achados":[]}""", 1000, 0, 100));
        });

        await using var ctx = db.Context();
        var app = db.AppService(ctx, settings, handler);
        var request = new CriarSolicitacaoAssistenteRequest(
            Guid.NewGuid(), AssistenteModo.Perguntar, "Analise o contrato.", 1,
            AssistenteOperacao.ResponderNoPainel, new(new(AssistenteEscopoDocumento.Nenhum)));

        var execution = await app.PrepararAsync(db.PecaId, request, default);
        try
        {
            await using (var competingCtx = db.Context())
            {
                var competingLimit = db.LimitService(competingCtx, settings);
                await competingLimit.ReservarAsync(
                    new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Concorrente", 1_000, 1_000),
                    default);
            }

            var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => app.GerarAsync(execution, default));
            Assert.Equal("AI_MONTHLY_LIMIT_INSUFFICIENT", ex.Code);
            Assert.Equal(429, ex.StatusCode);
        }
        finally
        {
            await app.FinalizarAsync(execution);
        }

        Assert.Equal(0, providerCalls);
        await using var verify = db.Context();
        Assert.Empty(await verify.AiUsageLedger.AsNoTracking().ToArrayAsync());
        Assert.Single(await verify.AiUsageReservations.AsNoTracking().ToArrayAsync()); // only the competing reservation
    }

    [ReviewPostgresFact]
    public async Task FerramentasPrimeiroTurno_FalhaAntesDaPrimeiraChamadaFisica_NaoCriaLedgerOuPoluiOrcamentoDuravel()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 0.015m);
        var docSettings = Options.Create(new AssistenteDocumentSettings { Enabled = true, RetrievalEnabled = true });

        // Part A: BeforeFinancialGeneration rejects (balance consumed between PrepararAsync and GerarAsync round 0)
        var providerCallsA = 0;
        var handlerA = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":1000}""") });
            providerCallsA++;
            return Task.FromResult(MemoryStream("""{"titulo":"R","texto":"T","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false,"coberturaPretendida":"Relevantes"}""", 1000, 0, 100));
        });

        await using (var ctxA = db.Context())
        {
            var repo = new AssistenteWorkspaceRepository(ctxA);
            var pieces = new PecaRepository(ctxA);
            var auth = new TestAuthContext(db.OrgId, db.UserId);
            var limitSvc = db.LimitService(ctxA, settings);
            var monitor = new Monitor(settings);
            var docs = new AssistenteDocumentService(ctxA, repo, pieces, new InMemoryStorage(), auth, db.Access, new Transparency(), docSettings);
            var chat = new AssistenteChatService(repo, pieces, auth, db.Access, new Transparency(), limitSvc, monitor, docs);
            var provider = new OpenAiAssistenteIaProvider(new HttpClient(handlerA) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(settings));
            var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(ctxA), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
            var app = new AssistenteIaApplicationService(
                pieces, new CasoRepository(ctxA), auth, provider, provider,
                new AssistenteContextBuilder(Options.Create(settings)), new AssistentePromptPolicy(),
                new AssistenteRequestRegistry(), db.Access, new Transparency(),
                new AssistenteUsageLimiter(monitor), usage, limitSvc, new CorrelationStub(),
                monitor, NullLogger<AssistenteIaApplicationService>.Instance, docSettings);

            var conv = await new AssistenteWorkspaceService(repo, pieces, auth, db.Access, new Transparency(), monitor, new AssistentePatchCompiler(), docs)
                .ObterOuCriarAsync(db.PecaId, default);
            var reqA = new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Consulte e responda.", 1).Solicitar();
            var startA = await chat.IniciarAsync(conv.Id, reqA, default);
            var toolsA = chat.CriarFerramentasDocumentais(startA, (_, _) => Task.CompletedTask);
            Assert.NotNull(toolsA);
            var execA = await app.PrepararAsync(db.PecaId, reqA, default, startA.Contexto, conversaLivre: true, toolsA);
            try
            {
                await using (var competingCtx = db.Context())
                {
                    await db.LimitService(competingCtx, settings).ReservarAsync(
                        new(db.OrgId, db.UserId, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Concorrente", 1_000, 1_000),
                        default);
                }

                var exA = await Assert.ThrowsAsync<AssistenteIaException>(() => app.GerarAsync(execA, default));
                Assert.Equal("AI_MONTHLY_LIMIT_INSUFFICIENT", exA.Code);
                await chat.FalharAsync(startA, exA.Code, true);
            }
            finally { await app.FinalizarAsync(execA); }
        }

        Assert.Equal(0, providerCallsA);
        await using (var verifyA = db.Context())
            Assert.Empty(await verifyA.AiUsageLedger.AsNoTracking().ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task FerramentasPrimeiroTurno_FalhaEmBeforeCallAntesDaPrimeiraChamada_NaoCriaLedgerOuPoluiOrcamentoDuravel()
    {
        await using var db = await P03Database.CreateAsync();
        var settings = PricedSettings(monthlyLimitUsd: 0.015m);
        var docSettings = Options.Create(new AssistenteDocumentSettings { Enabled = true, RetrievalEnabled = true });
        var providerCallsB = 0;
        var handlerB = new MemoryHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"input_tokens":1000}""") });
            providerCallsB++;
            return Task.FromResult(MemoryStream("""{"titulo":"R","texto":"T","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false,"coberturaPretendida":"Relevantes"}""", 1000, 0, 100));
        });

        Guid clientRequestIdB = Guid.NewGuid();
        await using (var ctxB = db.Context())
        {
            var repo = new AssistenteWorkspaceRepository(ctxB);
            var pieces = new PecaRepository(ctxB);
            var cases = new CasoRepository(ctxB);
            var auth = new TestAuthContext(db.OrgId, db.UserId);
            var limitSvc = db.LimitService(ctxB, settings);
            var monitor = new Monitor(settings);
            var docs = new AssistenteDocumentService(ctxB, repo, pieces, new InMemoryStorage(), auth, db.Access, new Transparency(), docSettings);
            var generations = new AssistenteGenerationService(ctxB, repo, pieces, cases, auth, db.Access, new Transparency(), limitSvc, docs, monitor, new AssistenteBackgroundIdentity(), new CorrelationStub());
            var chat = new AssistenteChatService(repo, pieces, auth, db.Access, new Transparency(), limitSvc, monitor, docs);
            var provider = new OpenAiAssistenteIaProvider(new HttpClient(handlerB) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(settings));
            var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(ctxB), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
            var app = new AssistenteIaApplicationService(
                pieces, cases, auth, provider, provider,
                new AssistenteContextBuilder(Options.Create(settings)), new AssistentePromptPolicy(),
                new AssistenteRequestRegistry(), db.Access, new Transparency(),
                new AssistenteUsageLimiter(monitor), usage, limitSvc, new CorrelationStub(),
                monitor, NullLogger<AssistenteIaApplicationService>.Instance, docSettings);

            var conv = await new AssistenteWorkspaceService(repo, pieces, auth, db.Access, new Transparency(), monitor, new AssistentePatchCompiler(), docs)
                .ObterOuCriarAsync(db.PecaId, default);
            var reqB = new EnviarInteracaoLivreRequest(clientRequestIdB, "Consulte os anexos e responda.", 1).Solicitar();
            var startB = await chat.IniciarAsync(conv.Id, reqB, default);
            var toolsB = chat.CriarFerramentasDocumentais(startB, (_, _) => Task.CompletedTask);
            Assert.NotNull(toolsB);
            var originalBeforeCall = toolsB!.BeforeCall;
            var passPreparar = true;
            toolsB.BeforeCall = async ct =>
            {
                if (passPreparar)
                {
                    if (originalBeforeCall is not null) await originalBeforeCall(ct);
                    return;
                }
                throw new AssistenteIaException(409, "AI_GENERATION_LEASE_LOST", "Lease perdida antes da chamada.");
            };

            var execB = await app.PrepararAsync(db.PecaId, reqB, default, startB.Contexto, conversaLivre: true, toolsB);
            passPreparar = false;

            var nowB = DateTime.UtcNow;
            var job = new AssistenteGeracao
            {
                Id = Guid.NewGuid(),
                OrganizationId = db.OrgId,
                UserId = db.UserId,
                PecaId = db.PecaId,
                ConversaId = conv.Id,
                PedidoId = startB.Pedido.Id,
                RespostaId = startB.Resposta.Id,
                ClientRequestId = clientRequestIdB,
                CorrelationId = Guid.NewGuid(),
                Estado = "EmExecucao",
                FontesIds = JsonDocument.Parse("[]"),
                CriadaEm = nowB,
                AtualizadaEm = nowB,
                PrazoEm = nowB.AddMinutes(30)
            };
            ctxB.Add(job);
            await ctxB.SaveChangesAsync();

            try
            {
                var exB = await Assert.ThrowsAsync<AssistenteIaException>(() => app.GerarAsync(execB, default));
                Assert.Equal("AI_GENERATION_LEASE_LOST", exB.Code);
            }
            finally { await app.FinalizarAsync(execB); }

            var budget = await generations.BudgetAsync(job, default);
            Assert.Equal((0, 0), budget);
        }

        Assert.Equal(0, providerCallsB);
        await using var verify = db.Context();
        Assert.Empty(await verify.AiUsageLedger.AsNoTracking().ToArrayAsync());
    }

    private sealed class AccountLockBarrier : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("SELECT * FROM ai_monthly_accounts", StringComparison.Ordinal) &&
                command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(ct);
            }
            return result;
        }
    }

    private sealed class CapturingUsageRepository : IAiUsageLedgerRepository
    {
        public List<AiUsageLedgerEntry> Entries { get; } = [];
        public Task AdicionarAsync(AiUsageLedgerEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FailOnSecondLedgerRepository(JuridicoAiDbContext ctx) : IAiUsageLedgerRepository
    {
        private int _calls;
        public async Task AdicionarAsync(AiUsageLedgerEntry entry, CancellationToken cancellationToken)
        {
            _calls++;
            if (_calls == 2) throw new IOException("Falha sintética ao persistir ledger do turno 2.");
            ctx.AiUsageLedger.Add(entry);
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class InMemoryStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new();
        public Task SaveAsync(string storageId, Stream content, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            _files[storageId] = ms.ToArray();
            return Task.CompletedTask;
        }
        public Task<Stream> GetAsync(string storageId, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(_files[storageId]));
        public Task DeleteAsync(string storageId, CancellationToken cancellationToken) { _files.Remove(storageId); return Task.CompletedTask; }
    }

    private static HttpResponseMessage MemoryStream(string output, int inputTokens, int cachedTokens, int outputTokens, int? cacheWriteTokens = null)
    {
        var delta = JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = output });
        var completed = JsonSerializer.Serialize(new
        {
            type = "response.completed",
            response = new
            {
                status = "completed",
                model = "gpt-test-p03",
                usage = new { input_tokens = inputTokens, input_tokens_details = InputTokenDetails(cachedTokens, cacheWriteTokens), output_tokens = outputTokens }
            }
        });
        return new(HttpStatusCode.OK) { Content = new StringContent($"data: {delta}\n\ndata: {completed}\n\n", Encoding.UTF8, "text/event-stream") };
    }

    private static HttpResponseMessage MemoryStreamTool(string outputItemJson, int inputTokens, int cachedTokens, int outputTokens, int? cacheWriteTokens = null)
    {
        var item = JsonDocument.Parse(outputItemJson).RootElement;
        var completed = JsonSerializer.Serialize(new
        {
            type = "response.completed",
            response = new
            {
                status = "completed",
                model = "gpt-test-p03",
                output = new[] { item },
                usage = new { input_tokens = inputTokens, input_tokens_details = InputTokenDetails(cachedTokens, cacheWriteTokens), output_tokens = outputTokens }
            }
        });
        return new(HttpStatusCode.OK) { Content = new StringContent($"data: {completed}\n\n", Encoding.UTF8, "text/event-stream") };
    }

    private static Dictionary<string, object> InputTokenDetails(int cachedTokens, int? cacheWriteTokens)
    {
        var details = new Dictionary<string, object> { ["cached_tokens"] = cachedTokens };
        if (cacheWriteTokens is { } written)
        {
            details["cache_write_tokens"] = written;
            details["cache_write_tokens_details"] = new { ttl_30m_tokens = written, ttl_12h_tokens = 0 };
        }
        return details;
    }

    private sealed class MemoryHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return send(request);
        }
    }

    private sealed class Monitor(AssistenteIaSettings settings) : IOptionsMonitor<AssistenteIaSettings>
    {
        public AssistenteIaSettings CurrentValue => settings;
        public AssistenteIaSettings Get(string? name) => settings;
        public IDisposable? OnChange(Action<AssistenteIaSettings, string?> listener) => null;
    }

    private sealed class Features : IAssistenteFeatureAccess
    {
        public Task<AssistenteFeatureSnapshot> ObterAsync(Guid org, CancellationToken ct) =>
            Task.FromResult(new AssistenteFeatureSnapshot(true,
                [OrganizationFeatureEnum.AI_ASSISTANT, OrganizationFeatureEnum.AI_EDIT, OrganizationFeatureEnum.AI_DOCUMENT_GENERATION],
                Enum.GetValues<AssistenteModo>()));
        public Task ExigirModoAsync(Guid org, AssistenteModo mode, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Transparency : IAssistenteTransparencyService
    {
        public Task ExigirConfirmacaoAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<AssistenteTransparenciaDto> ObterAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<AssistenteTransparenciaDto> ConfirmarAsync(ConfirmarAssistenteTransparenciaRequest req, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CorrelationStub : ICorrelationContext
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
    }

    private sealed class FakeTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _now = initial;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class P03Database(string connectionString, string dbName) : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;
        public Guid OrgId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid PecaId { get; } = Guid.NewGuid();
        public Features Access { get; } = new();

        public JuridicoAiDbContext Context(IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<JuridicoAiDbContext>().UseNpgsql(ConnectionString);
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            return new(builder.Options);
        }

        public AssistenteMonthlyLimitService LimitService(
            JuridicoAiDbContext ctx,
            AssistenteIaSettings settings,
            TimeProvider? time = null,
            string environmentName = "Testing") =>
            new(ctx, new Monitor(settings), new TestHostEnvironment(environmentName),
                NullLogger<AssistenteMonthlyLimitService>.Instance, time);

        public AssistenteIaApplicationService AppService(
            JuridicoAiDbContext ctx,
            AssistenteIaSettings settings,
            HttpMessageHandler handler,
            string environmentName = "Testing",
            AssistenteDocumentService? docs = null)
        {
            var provider = new OpenAiAssistenteIaProvider(new HttpClient(handler) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(settings));
            var auth = new TestAuthContext(OrgId, UserId);
            var monitor = new Monitor(settings);
            var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(ctx), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
            var limitSvc = LimitService(ctx, settings, environmentName: environmentName);
            return new AssistenteIaApplicationService(
                new PecaRepository(ctx), new CasoRepository(ctx), auth, provider, provider,
                new AssistenteContextBuilder(Options.Create(settings)), new AssistentePromptPolicy(),
                new AssistenteRequestRegistry(), Access, new Transparency(),
                new AssistenteUsageLimiter(monitor), usage, limitSvc, new CorrelationStub(),
                monitor, NullLogger<AssistenteIaApplicationService>.Instance);
        }

        public AssistenteDocumentAnalysisProcessor AnalysisProcessor(
            JuridicoAiDbContext ctx,
            AssistenteIaSettings settings,
            AssistenteDocumentSettings docSettings,
            IFileStorage storage,
            HttpMessageHandler handler)
        {
            var provider = new OpenAiAssistenteIaProvider(new HttpClient(handler) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(settings));
            var auth = new TestAuthContext(OrgId, UserId);
            var monitor = new Monitor(settings);
            var docOpts = Options.Create(docSettings);
            var repo = new AssistenteWorkspaceRepository(ctx);
            var pieces = new PecaRepository(ctx);
            var limitSvc = LimitService(ctx, settings);
            var docs = new AssistenteDocumentService(ctx, repo, pieces, storage, auth, Access, new Transparency(), docOpts);
            var analyses = new AssistenteDocumentAnalysisService(ctx, repo, pieces, auth, Access, new Transparency(), limitSvc, docs, docOpts, monitor, new AssistenteBackgroundIdentity());
            var chat = new AssistenteChatService(repo, pieces, auth, Access, new Transparency(), limitSvc, monitor, docs, analysis: analyses);
            var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(ctx), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
            var limiter = new AssistenteUsageLimiter(monitor);
            var generator = new AssistenteIaApplicationService(pieces, new CasoRepository(ctx), auth, provider, provider,
                new AssistenteContextBuilder(Options.Create(settings)), new AssistentePromptPolicy(), new AssistenteRequestRegistry(),
                Access, new Transparency(), limiter, usage, limitSvc, new CorrelationStub(), monitor, NullLogger<AssistenteIaApplicationService>.Instance, docOpts);
            return new AssistenteDocumentAnalysisProcessor(ctx, analyses, docs, chat, generator, provider, provider, limiter, usage, limitSvc, docOpts, monitor);
        }

        public AssistenteGenerationProcessor GenerationProcessor(
            JuridicoAiDbContext ctx,
            AssistenteIaSettings settings,
            AssistenteDocumentSettings docSettings,
            IFileStorage storage,
            HttpMessageHandler handler)
        {
            var provider = new OpenAiAssistenteIaProvider(new HttpClient(handler) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(settings));
            var auth = new TestAuthContext(OrgId, UserId);
            var monitor = new Monitor(settings);
            var docOpts = Options.Create(docSettings);
            var repo = new AssistenteWorkspaceRepository(ctx);
            var pieces = new PecaRepository(ctx);
            var cases = new CasoRepository(ctx);
            var limitSvc = LimitService(ctx, settings);
            var docs = new AssistenteDocumentService(ctx, repo, pieces, storage, auth, Access, new Transparency(), docOpts);
            var generations = new AssistenteGenerationService(ctx, repo, pieces, cases, auth, Access, new Transparency(), limitSvc, docs, monitor, new AssistenteBackgroundIdentity(), new CorrelationStub());
            var chat = new AssistenteChatService(repo, pieces, auth, Access, new Transparency(), limitSvc, monitor, docs, generations: generations);
            var usage = new AssistenteUsageRecorder(new AiUsageLedgerRepository(ctx), monitor, NullLogger<AssistenteUsageRecorder>.Instance);
            var limiter = new AssistenteUsageLimiter(monitor);
            var generator = new AssistenteIaApplicationService(pieces, cases, auth, provider, provider,
                new AssistenteContextBuilder(Options.Create(settings)), new AssistentePromptPolicy(), new AssistenteRequestRegistry(),
                Access, new Transparency(), limiter, usage, limitSvc, new CorrelationStub(), monitor, NullLogger<AssistenteIaApplicationService>.Instance, docOpts);
            return new AssistenteGenerationProcessor(ctx, generations, chat, generator, provider, provider, Access, usage, limiter, limitSvc, monitor);
        }

        public async Task<Guid> CreateSecondOrganizationAsync()
        {
            var secondOrg = Guid.NewGuid();
            await using var ctx = Context();
            var now = DateTime.UtcNow;
            ctx.Organizations.Add(new Organization { Id = secondOrg, Nome = "Org B", DataCriacao = now, DataAtualizacao = now });
            await ctx.SaveChangesAsync();
            return secondOrg;
        }

        public static async Task<P03Database> CreateAsync()
        {
            var admin = Environment.GetEnvironmentVariable("JURIDICOAI_REVIEW_TEST_POSTGRES")!;
            var name = "p03_" + Guid.NewGuid().ToString("N");
            await using (var cn = new NpgsqlConnection(admin))
            {
                await cn.OpenAsync();
                await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", cn);
                await cmd.ExecuteNonQueryAsync();
            }

            var cs = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false }.ConnectionString;
            var db = new P03Database(cs, name);
            await using var ctx = db.Context();
            await ctx.Database.MigrateAsync();
            var now = DateTime.UtcNow;
            var caso = new Caso { Id = Guid.NewGuid(), OrganizationId = db.OrgId, CreatedById = db.UserId, UpdatedById = db.UserId, Titulo = "Caso P03", DataCriacao = now, DataAtualizacao = now };
            ctx.Organizations.Add(new Organization { Id = db.OrgId, Nome = "Org A", DataCriacao = now, DataAtualizacao = now });
            ctx.Users.Add(new User { Id = db.UserId, Nome = "Operador", Email = $"{db.UserId:N}@example.test", PasswordHash = "x", Status = UserStatusEnum.Active, DataCriacao = now, DataAtualizacao = now });
            ctx.Casos.Add(caso);
            ctx.Pecas.Add(new Peca
            {
                Id = db.PecaId, CasoId = caso.Id, OrganizationId = db.OrgId, CreatedById = db.UserId, UpdatedById = db.UserId,
                Titulo = "Peça P03", ConteudoJson = JsonDocument.Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Texto"}]}]}"""),
                PageConfig = JsonDocument.Parse("{}"), Styles = JsonDocument.Parse("{}"), DataCriacao = now, DataAtualizacao = now
            });
            await ctx.SaveChangesAsync();
            return db;
        }

        public async ValueTask DisposeAsync()
        {
            await using var cn = new NpgsqlConnection(Environment.GetEnvironmentVariable("JURIDICOAI_REVIEW_TEST_POSTGRES"));
            await cn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"DROP DATABASE {dbName} WITH (FORCE)", cn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private sealed class P03HttpTestApi(WebApplication app, Uri baseAddress) : IAsyncDisposable
    {
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter() } };

        public static async Task<P03HttpTestApi> CreateAsync(
            string connectionString,
            AssistenteIaSettings settings,
            Func<HttpRequestMessage, Task<HttpResponseMessage>>? providerSend = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
                ApplicationName = typeof(AssistenteController).Assembly.FullName
            });
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
            builder.Services.AddControllers().AddApplicationPart(typeof(AssistenteController).Assembly)
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<JuridicoAiDbContext>(o => o.UseNpgsql(connectionString));
            builder.Services.AddAuthentication(P03TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, P03TestAuthHandler>(P03TestAuthHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<IOptions<AssistenteIaSettings>>(Options.Create(settings));
            builder.Services.AddSingleton<IOptionsMonitor<AssistenteIaSettings>>(new Monitor(settings));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddScoped<IAssistenteMonthlyLimitService, AssistenteMonthlyLimitService>();
            builder.Services.AddScoped<IAssistenteUsageRecorder, AssistenteUsageRecorder>();
            builder.Services.AddScoped<IAiUsageLedgerRepository, AiUsageLedgerRepository>();
            builder.Services.AddScoped<IPecaRepository, PecaRepository>();
            builder.Services.AddScoped<ICasoRepository, CasoRepository>();
            builder.Services.AddScoped<IAuthContext, HttpAuthContext>();
            builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
            builder.Services.AddScoped<AssistenteContextBuilder>();
            builder.Services.AddSingleton<AssistentePromptPolicy>();
            builder.Services.AddSingleton<AssistenteRequestRegistry>();
            builder.Services.AddSingleton<IAssistenteUsageLimiter, AssistenteUsageLimiter>();
            builder.Services.AddSingleton<IAssistenteFeatureAccess, Features>();
            builder.Services.AddSingleton<IAssistenteTransparencyService, Transparency>();
            var httpClient = providerSend is null ? new HttpClient() : new HttpClient(new MemoryHandler(providerSend)) { BaseAddress = new("https://synthetic.test/v1/") };
            builder.Services.AddScoped<IAssistenteIaProvider>(_ => new OpenAiAssistenteIaProvider(httpClient, Options.Create(settings)));
            builder.Services.AddScoped<IAssistenteTokenCounter>(_ => new OpenAiAssistenteIaProvider(httpClient, Options.Create(settings)));
            builder.Services.AddScoped<AssistenteIaApplicationService>();

            var app = builder.Build();
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<ApiExceptionMiddleware>();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new Uri(address));
        }

        public HttpClient CreateAnonymousClient() => new() { BaseAddress = baseAddress };

        public HttpClient CreateClient(Guid orgId, Guid userId)
        {
            var client = CreateAnonymousClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(P03TestAuthHandler.SchemeName, "ok");
            client.DefaultRequestHeaders.Add(P03TestAuthHandler.OrgHeader, orgId.ToString("D"));
            client.DefaultRequestHeaders.Add(P03TestAuthHandler.UserHeader, userId.ToString("D"));
            client.DefaultRequestHeaders.Add(P03TestAuthHandler.SessionHeader, Guid.NewGuid().ToString("D"));
            return client;
        }

        public ValueTask DisposeAsync() => app.DisposeAsync();
    }

    private sealed class P03TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "P03Test";
        public const string OrgHeader = "X-Test-Organization";
        public const string UserHeader = "X-Test-User";
        public const string SessionHeader = "X-Test-Session";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var auth) || string.IsNullOrWhiteSpace(auth))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[]
            {
                new Claim("sub", Request.Headers[UserHeader].ToString()),
                new Claim("organizationId", Request.Headers[OrgHeader].ToString()),
                new Claim("sessionId", Request.Headers[SessionHeader].ToString()),
                new Claim("role", MembershipRoleEnum.Owner.ToString())
            };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
        }
    }
}
