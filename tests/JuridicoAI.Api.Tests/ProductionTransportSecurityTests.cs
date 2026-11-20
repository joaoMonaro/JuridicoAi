using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Http;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class ProductionTransportSecurityTests
{
    [Fact]
    public async Task Production_HttpRecusadoAntesDosEndpointsMesmoComCabecalhosForjados()
    {
        await using var api = await TestApi.CreateAsync(Environments.Production);
        using var client = api.Client(https: false);
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("Forwarded", "proto=https");

        foreach (var path in new[] { "/auth/login", "/auth/refresh", "/auth/pessoal/login", "/health/live" })
        {
            using var response = await client.PostAsync(path, new StringContent("{}"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("HTTPS_REQUIRED", problem.RootElement.GetProperty("code").GetString());
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.Null(response.Headers.Location);
        }

        Assert.Equal(0, api.EndpointCalls);
    }

    [Fact]
    public async Task Production_HttpsComTlsExecutaEndpoint()
    {
        await using var api = await TestApi.CreateAsync(Environments.Production);
        using var client = api.Client(https: true);

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, api.EndpointCalls);
    }

    [Fact]
    public async Task Development_HttpLocalContinuaDisponivel()
    {
        await using var api = await TestApi.CreateAsync(Environments.Development);
        using var client = api.Client(https: false);

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, api.EndpointCalls);
    }

    [Theory]
    [InlineData("Production", false, false)]
    [InlineData("Production", true, true)]
    [InlineData("Development", false, true)]
    [InlineData("Development", true, true)]
    public async Task Inicializacao_ExigeCookieSecureSomenteEmProducao(
        string environment, bool secureCookie, bool starts)
    {
        const string fictitiousSecret = "segredo-ficticio-exclusivo-deste-teste-123";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment,
            DisableDefaults = true
        });
        builder.Services.AddSingleton<IValidateOptions<AuthSettings>, AuthSettingsValidator>();
        builder.Services.AddOptions<AuthSettings>().Bind(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSecret"] = fictitiousSecret,
                ["SecureRefreshCookie"] = secureCookie.ToString()
            }).Build()).ValidateOnStart();
        using var host = builder.Build();

        if (starts)
        {
            await host.StartAsync();
            await host.StopAsync();
        }
        else
        {
            var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
            Assert.Contains("Auth:SecureRefreshCookie", exception.Message);
            Assert.DoesNotContain(fictitiousSecret, exception.Message);
        }
    }

    [Fact]
    public void Validacao_PreservaExigenciasDoSegredoJwtEDosPrazos()
    {
        var validator = new AuthSettingsValidator(new TestHostEnvironment("Production"));

        var result = validator.Validate(null, new AuthSettings
        {
            JwtSecret = "curto", AccessTokenTtlMinutes = 0, RefreshTokenTtlDays = 0, SecureRefreshCookie = true
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, error => error.Contains("Auth:JwtSecret"));
        Assert.Contains(result.Failures!, error => error.Contains("TTLs"));
    }

    private sealed class TestApi(WebApplication app, X509Certificate2 certificate) : IAsyncDisposable
    {
        public int EndpointCalls;

        public HttpClient Client(bool https)
        {
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
            return new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                // Confiança restrita ao certificado efêmero desta instância de teste.
                ServerCertificateCustomValidationCallback = (_, remote, _, _) => remote?.Thumbprint == certificate.Thumbprint
            }) { BaseAddress = new Uri(addresses.Single(address => new Uri(address).Scheme == (https ? "https" : "http"))) };
        }

        public static async Task<TestApi> CreateAsync(string environment, string[]? origins = null,
            IReadOnlyDictionary<string, string?>? proxyConfiguration = null)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection((origins ?? ["https://app.example.test"])
                .Select((origin, index) => new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin)));
            if (proxyConfiguration is not null) builder.Configuration.AddInMemoryCollection(proxyConfiguration);
            builder.Logging.ClearProviders();
            builder.Services.AddOptions<ForwardedHeadersOptions>().PostConfigure<IConfiguration>(
                (options, configuration) => (configuration.GetSection("ReverseProxy").Get<ReverseProxySettings>() ?? new())
                    .ConfigureForwardedHeaders(options));
            builder.Services.AddRateLimiter(AuthRateLimitPolicy.Configure);
            builder.Services.AddSingleton<FrontendOriginPolicy>();
            builder.Services.AddCors();
            builder.Services.AddOptions<CorsOptions>().Configure<FrontendOriginPolicy>(
                (options, policy) => policy.ConfigureCors(options));
            builder.WebHost.ConfigureKestrel(server =>
            {
                server.Listen(IPAddress.Loopback, 0);
                server.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate));
            });
            var app = builder.Build();
            var api = new TestApi(app, certificate);
            app.UseForwardedHeaders();
            app.UseMiddleware<ProductionHttpsMiddleware>();
            app.UseCors(FrontendOriginPolicy.PolicyName);
            app.UseRateLimiter();
            Task Probe(HttpContext context)
            {
                Interlocked.Increment(ref api.EndpointCalls);
                context.Response.Headers["X-Test-Remote-IP"] = context.Connection.RemoteIpAddress?.ToString();
                context.Response.Headers["X-Test-Scheme"] = context.Request.Scheme;
                context.Response.Headers["X-Test-Host"] = context.Request.Host.ToString();
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            }
            app.MapPost("/auth-probe", Probe).RequireRateLimiting(AuthRateLimitPolicy.Name);
            app.MapFallback(Probe);
            try
            {
                await app.StartAsync();
                return api;
            }
            catch
            {
                await api.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await app.DisposeAsync();
            certificate.Dispose();
        }
    }
}
