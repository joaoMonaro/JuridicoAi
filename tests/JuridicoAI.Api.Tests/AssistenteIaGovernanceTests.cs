using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Http;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class AssistenteIaGovernanceTests
{
    [Fact]
    public async Task FeatureAccess_FalhaFechadoEIsolaConfiguracaoPorOrganization()
    {
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var repository = new FeatureRepositoryStub();
        repository.Features[organizationA] = new HashSet<OrganizationFeatureEnum>
            { OrganizationFeatureEnum.AI_ASSISTANT, OrganizationFeatureEnum.AI_EDIT };
        repository.Features[organizationB] = new HashSet<OrganizationFeatureEnum>
            { OrganizationFeatureEnum.AI_ASSISTANT };
        var service = new AssistenteFeatureAccessService(
            repository,
            new OperationalControlStub(),
            new StaticOptionsMonitor<AssistenteIaSettings>(new AssistenteIaSettings { Enabled = true }));

        var snapshotA = await service.ObterAsync(organizationA, default);
        var snapshotB = await service.ObterAsync(organizationB, default);
        var unknown = await service.ObterAsync(Guid.NewGuid(), default);

        Assert.Contains(AssistenteModo.Redigir, snapshotA.Modos);
        Assert.DoesNotContain(AssistenteModo.Redigir, snapshotB.Modos);
        Assert.False(unknown.Habilitado);
        var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            service.ExigirModoAsync(organizationB, AssistenteModo.Revisar, default));
        Assert.Equal("AI_FEATURE_NOT_ENABLED", exception.Code);
        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
    }

    [Fact]
    public async Task FeatureAccess_KillSwitchGlobalRetornaIndisponivel()
    {
        var operationalControl = new OperationalControlStub();
        var service = new AssistenteFeatureAccessService(
            new FeatureRepositoryStub(),
            operationalControl,
            new StaticOptionsMonitor<AssistenteIaSettings>(new AssistenteIaSettings { Enabled = false }));

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            service.ExigirModoAsync(Guid.NewGuid(), AssistenteModo.Perguntar, default));

        Assert.Equal("AI_UNAVAILABLE", exception.Code);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
        Assert.Equal(0, operationalControl.ReadCount);
    }

    [Fact]
    public async Task FeatureAccess_PropagaRevogacaoERollbackSemAfetarOutraOrganization()
    {
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var repository = new FeatureRepositoryStub();
        repository.Features[organizationA] = new HashSet<OrganizationFeatureEnum>
            { OrganizationFeatureEnum.AI_ASSISTANT, OrganizationFeatureEnum.AI_EDIT };
        repository.Features[organizationB] = new HashSet<OrganizationFeatureEnum>();
        var service = new AssistenteFeatureAccessService(
            repository,
            new OperationalControlStub(),
            new StaticOptionsMonitor<AssistenteIaSettings>(new AssistenteIaSettings { Enabled = true }));

        await service.ExigirModoAsync(organizationA, AssistenteModo.Redigir, default);

        repository.Features[organizationA] = new HashSet<OrganizationFeatureEnum>
            { OrganizationFeatureEnum.AI_ASSISTANT };
        var revoked = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            service.ExigirModoAsync(organizationA, AssistenteModo.Redigir, default));
        var organizationBState = await service.ObterAsync(organizationB, default);

        repository.Features[organizationA] = new HashSet<OrganizationFeatureEnum>
            { OrganizationFeatureEnum.AI_ASSISTANT, OrganizationFeatureEnum.AI_EDIT };
        await service.ExigirModoAsync(organizationA, AssistenteModo.Redigir, default);

        Assert.Equal("AI_FEATURE_NOT_ENABLED", revoked.Code);
        Assert.False(organizationBState.Habilitado);
    }

    [Fact]
    public async Task FeatureAccess_PropagaKillSwitchERollbackGlobal()
    {
        var organizationId = Guid.NewGuid();
        var repository = new FeatureRepositoryStub();
        repository.Features[organizationId] = new HashSet<OrganizationFeatureEnum>
            { OrganizationFeatureEnum.AI_ASSISTANT };
        var operationalControl = new OperationalControlStub();
        var service = new AssistenteFeatureAccessService(
            repository,
            operationalControl,
            new StaticOptionsMonitor<AssistenteIaSettings>(new() { Enabled = true }));

        Assert.True((await service.ObterAsync(organizationId, default)).Habilitado);

        operationalControl.Enabled = false;
        Assert.False((await service.ObterAsync(organizationId, default)).Habilitado);
        var disabled = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            service.ExigirModoAsync(organizationId, AssistenteModo.Perguntar, default));

        operationalControl.Enabled = true;
        Assert.True((await service.ObterAsync(organizationId, default)).Habilitado);
        Assert.Equal("AI_UNAVAILABLE", disabled.Code);
    }

    [Fact]
    public async Task Limiter_BloqueiaTerceiraOperacaoSimultaneaDoMesmoUsuario()
    {
        using var limiter = new AssistenteUsageLimiter(new StaticOptionsMonitor<AssistenteIaSettings>(new()
        {
            RequestsPerUserPerMinute = 10,
            ConcurrentRequestsPerUser = 2,
            RequestsPerOrganizationPerMinute = 30,
            ConcurrentRequestsPerOrganization = 6
        }));
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var first = await limiter.AdquirirAsync(organizationId, userId, default);
        await using var second = await limiter.AdquirirAsync(organizationId, userId, default);

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(async () =>
            await limiter.AdquirirAsync(organizationId, userId, default));

        Assert.Equal("AI_USER_CONCURRENCY_LIMITED", exception.Code);
    }

    [Fact]
    public async Task Limiter_AplicaRateLimitCompartilhadoDaOrganization()
    {
        using var limiter = new AssistenteUsageLimiter(new StaticOptionsMonitor<AssistenteIaSettings>(new()
        {
            RequestsPerUserPerMinute = 10,
            ConcurrentRequestsPerUser = 2,
            RequestsPerOrganizationPerMinute = 2,
            ConcurrentRequestsPerOrganization = 6
        }));
        var organizationId = Guid.NewGuid();
        await using (await limiter.AdquirirAsync(organizationId, Guid.NewGuid(), default)) { }
        await using (await limiter.AdquirirAsync(organizationId, Guid.NewGuid(), default)) { }

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(async () =>
            await limiter.AdquirirAsync(organizationId, Guid.NewGuid(), default));

        Assert.Equal("AI_ORGANIZATION_RATE_LIMITED", exception.Code);
    }

    [Fact]
    public async Task Limiter_InformaRetryAfterQuandoJanelaDoUsuarioEsgota()
    {
        using var limiter = new AssistenteUsageLimiter(new StaticOptionsMonitor<AssistenteIaSettings>(new()
        {
            RequestsPerUserPerMinute = 1,
            ConcurrentRequestsPerUser = 2,
            RequestsPerOrganizationPerMinute = 30,
            ConcurrentRequestsPerOrganization = 6
        }));
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using (await limiter.AdquirirAsync(organizationId, userId, default)) { }

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(async () =>
            await limiter.AdquirirAsync(organizationId, userId, default));

        Assert.Equal("AI_USER_RATE_LIMITED", exception.Code);
        Assert.NotNull(exception.RetryAfter);
        Assert.True(exception.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task Limiter_BloqueiaConcorrenciaAgregadaDaOrganizationEIsolaOutraOrganization()
    {
        using var limiter = new AssistenteUsageLimiter(new StaticOptionsMonitor<AssistenteIaSettings>(new()
        {
            RequestsPerUserPerMinute = 10,
            ConcurrentRequestsPerUser = 2,
            RequestsPerOrganizationPerMinute = 30,
            ConcurrentRequestsPerOrganization = 2
        }));
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        await using var first = await limiter.AdquirirAsync(organizationA, Guid.NewGuid(), default);
        await using var second = await limiter.AdquirirAsync(organizationA, Guid.NewGuid(), default);

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(async () =>
            await limiter.AdquirirAsync(organizationA, Guid.NewGuid(), default));
        await using var otherOrganization = await limiter.AdquirirAsync(organizationB, Guid.NewGuid(), default);

        Assert.Equal("AI_ORGANIZATION_CONCURRENCY_LIMITED", exception.Code);
    }

    [Fact]
    public async Task Limiter_LiberaVagasDeUsuarioEOrganizationAoFinalizar()
    {
        using var limiter = new AssistenteUsageLimiter(new StaticOptionsMonitor<AssistenteIaSettings>(new()
        {
            RequestsPerUserPerMinute = 10,
            ConcurrentRequestsPerUser = 1,
            RequestsPerOrganizationPerMinute = 30,
            ConcurrentRequestsPerOrganization = 1
        }));
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var active = await limiter.AdquirirAsync(organizationId, userId, default);

        var exception = await Assert.ThrowsAsync<AssistenteIaException>(async () =>
            await limiter.AdquirirAsync(organizationId, userId, default));
        await active.DisposeAsync();
        await using var next = await limiter.AdquirirAsync(organizationId, userId, default);

        Assert.Equal("AI_USER_CONCURRENCY_LIMITED", exception.Code);
    }

    [Fact]
    public async Task ApiException_RetornaRetryAfterNoRateLimitLocal()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/pecas/00000000-0000-0000-0000-000000000001/assistente/solicitacoes";
        context.Response.Body = new MemoryStream();
        var middleware = new ApiExceptionMiddleware(
            _ => throw new AssistenteIaException(
                StatusCodes.Status429TooManyRequests,
                "AI_USER_RATE_LIMITED",
                "Limite atingido.",
                retryable: true,
                retryAfter: TimeSpan.FromSeconds(12.2)),
            NullLogger<ApiExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("13", context.Response.Headers["Retry-After"]);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("AI_USER_RATE_LIMITED", body);
    }

    [Fact]
    public async Task ApiException_NaoRegistraExcecaoComConteudoSensivelNoFluxoDeIa()
    {
        const string sensitiveMarker = "JWT-SIGILOSO-DO-TESTE";
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/pecas/00000000-0000-0000-0000-000000000001/assistente/solicitacoes";
        context.Response.Body = new MemoryStream();
        var logger = new CaptureLogger<ApiExceptionMiddleware>();
        var middleware = new ApiExceptionMiddleware(
            _ => throw new InvalidOperationException(sensitiveMarker),
            logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.DoesNotContain(logger.Messages, x => x.Contains(sensitiveMarker, StringComparison.Ordinal));
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain(sensitiveMarker, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorrelationId_PreservaUuidValidoNaRespostaENoContexto()
    {
        var expected = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationHeaders.Name] = expected.ToString();
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(expected, context.Items[CorrelationHeaders.ItemKey]);
        Assert.Equal(expected.ToString("D"), context.Response.Headers[CorrelationHeaders.Name]);
        Assert.Equal(expected.ToString("D"), context.TraceIdentifier);
    }

    [Fact]
    public async Task RequestSize_RejeitaPayloadAntesDoController()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/pecas/00000000-0000-0000-0000-000000000001/assistente/solicitacoes";
        context.Request.ContentLength = 101;
        var middleware = new AssistenteRequestSizeMiddleware(
            _ => Task.CompletedTask,
            new StaticOptionsMonitor<AssistenteIaSettings>(new() { MaxRequestBodyBytes = 100 }));

        var exception = await Assert.ThrowsAsync<BadHttpRequestException>(() => middleware.InvokeAsync(context));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, exception.StatusCode);
    }

    [Fact]
    public async Task UsageRecorder_CalculaCustoSeparandoEntradaEmCache()
    {
        var repository = new UsageRepositoryStub();
        var recorder = new AssistenteUsageRecorder(
            repository,
            new StaticOptionsMonitor<AssistenteIaSettings>(new()
            {
                InputCostPerMillionTokensUsd = 2m,
                CachedInputCostPerMillionTokensUsd = 0.2m,
                OutputCostPerMillionTokensUsd = 12m,
                PricingVersion = "teste"
            }),
            NullLogger<AssistenteUsageRecorder>.Instance);
        await recorder.RegistrarAsync(new AssistenteUsageRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            AssistenteModo.Perguntar, AssistenteOperacao.ResponderNoPainel, "OpenAI", "modelo", "prompt-v1",
            1, 100, 40, 20, "succeeded", null, 250), default);

        var entry = Assert.Single(repository.Entries);
        Assert.Equal(0.000368m, entry.EstimatedCostUsd);
        Assert.Equal("teste", entry.PricingVersion);
        Assert.Equal(40, entry.CachedInputTokens);
    }

    [Fact]
    public async Task UsageRecorder_NaoRegistraMensagemDaExcecaoDePersistencia()
    {
        const string sensitiveMarker = "CPF-SIGILOSO-52998224725";
        var repository = new UsageRepositoryStub
        {
            Error = new InvalidOperationException(sensitiveMarker)
        };
        var logger = new CaptureLogger<AssistenteUsageRecorder>();
        var recorder = new AssistenteUsageRecorder(
            repository,
            new StaticOptionsMonitor<AssistenteIaSettings>(new()),
            logger);

        await recorder.RegistrarAsync(new AssistenteUsageRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            AssistenteModo.Perguntar, null, "OpenAI", "modelo", "prompt-v1",
            1, null, null, null, "failed", "AI_PROVIDER_ERROR", 10), default);

        Assert.DoesNotContain(logger.Messages, x => x.Contains(sensitiveMarker, StringComparison.Ordinal));
        Assert.Contains(logger.Messages, x => x.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
    }

    private sealed class FeatureRepositoryStub : IOrganizationFeatureRepository
    {
        public Dictionary<Guid, IReadOnlySet<OrganizationFeatureEnum>> Features { get; } = [];
        public Task<IReadOnlySet<OrganizationFeatureEnum>> ListarHabilitadasAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Features.GetValueOrDefault(organizationId) ?? new HashSet<OrganizationFeatureEnum>());
        public Task DefinirAsync(Guid organizationId, OrganizationFeatureEnum feature, bool enabled, Guid updatedById, string reason, DateTime updatedAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class OperationalControlStub : IAiOperationalControlRepository
    {
        public bool Enabled { get; set; } = true;
        public int ReadCount { get; private set; }

        public Task<bool> ObterAssistenteHabilitadoAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Enabled);
        }

        public Task DefinirAssistenteHabilitadoAsync(
            bool enabled,
            Guid updatedById,
            string reason,
            DateTime updatedAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class UsageRepositoryStub : IAiUsageLedgerRepository
    {
        public List<AiUsageLedgerEntry> Entries { get; } = [];
        public Exception? Error { get; init; }
        public Task AdicionarAsync(AiUsageLedgerEntry entry, CancellationToken cancellationToken)
        {
            if (Error is not null) throw Error;
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (exception is not null) message += Environment.NewLine + exception;
            Messages.Add(message);
        }
    }

    private sealed class StaticOptionsMonitor<T>(T initialValue) : IOptionsMonitor<T>
    {
        private readonly T _value = initialValue;
        public T CurrentValue => _value;
        public T Get(string? name) => _value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

}
