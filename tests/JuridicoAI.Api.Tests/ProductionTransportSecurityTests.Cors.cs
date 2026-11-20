using System.Net;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Controllers.Auth;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Http;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class ProductionTransportSecurityTests
{
    [Fact]
    public async Task Cors_Production_OrigemExataPermiteCredenciaisEPreflightSemExecutarEndpoint()
    {
        await using var api = await TestApi.CreateAsync("Production");
        using var client = api.Client(https: true);
        using var request = Preflight("https://app.example.test");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://app.example.test", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.Contains("POST", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Methods")));
        Assert.Equal(0, api.EndpointCalls);

        using var get = new HttpRequestMessage(HttpMethod.Get, "/probe");
        get.Headers.Add("Origin", "https://app.example.test");
        using var actual = await client.SendAsync(get);
        Assert.Equal("https://app.example.test", Assert.Single(actual.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("Content-Disposition", string.Join(',', actual.Headers.GetValues("Access-Control-Expose-Headers")));
        Assert.Equal(1, api.EndpointCalls);
    }

    [Theory]
    [InlineData("https://other.example.test")]
    [InlineData("https://app.example.test.evil.test")]
    [InlineData("https://app.example.test:8443")]
    [InlineData("http://app.example.test")]
    [InlineData("https://localhost:5173")]
    [InlineData("null")]
    public async Task Cors_Production_PreflightDeOrigemNaoPermitidaNaoRecebePermissao(string origin)
    {
        await using var api = await TestApi.CreateAsync("Production");
        using var client = api.Client(https: true);
        using var request = Preflight(origin);

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.Equal(0, api.EndpointCalls);
    }

    [Fact]
    public async Task Cors_Production_ListaVaziaImpedeInicializacao()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => TestApi.CreateAsync("Production", []));
        Assert.Contains("Cors:AllowedOrigins", exception.Message);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.test")]
    [InlineData("http://app.example.test")]
    [InlineData("https://localhost:5173")]
    [InlineData("https://127.0.0.1:5173")]
    [InlineData("https://app.example.test/")]
    [InlineData("https://app.example.test/path")]
    [InlineData("https://app.example.test?query=1")]
    [InlineData("https://app.example.test#fragment")]
    [InlineData("https://user:password@example.test")]
    public void Cors_Production_RecusaConfiguracaoInvalidaSemExporValores(string origin)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Origins("Production", origin));
        Assert.Contains("Cors:AllowedOrigins[0]", exception.Message);
        Assert.DoesNotContain(origin, exception.Message);
    }

    [Fact]
    public async Task Cors_Development_PreservaLocalhostERecusaHostExternoNaoListado()
    {
        await using var api = await TestApi.CreateAsync("Development", []);
        using var client = api.Client(https: false);
        using var local = Preflight("http://localhost:5173");
        using var allowed = await client.SendAsync(local);
        Assert.Equal("http://localhost:5173", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));

        using var external = Preflight("http://localhost.evil.test:5173");
        using var denied = await client.SendAsync(external);
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(0, api.EndpointCalls);
    }

    [Theory]
    [InlineData("https://app.example.test", true)]
    [InlineData("https://other.example.test", false)]
    [InlineData("http://localhost:5173", false)]
    [InlineData(null, false)]
    public async Task SessaoPessoal_UsaAMesmaListaDeOrigensERecusaAntesDeConsultarDados(string? origin, bool allowed)
    {
        // Sem provider de banco: uma consulta inesperada falha sem conexão externa.
        using var db = new JuridicoAiDbContext(new DbContextOptionsBuilder<JuridicoAiDbContext>().Options);
        var settings = Options.Create(new AuthSettings { JwtSecret = new string('t', 64) });
        var service = new PersonalHistoryAuthService(db, new Argon2idPasswordHasher(), new JwtTokenService(settings), settings);
        var http = new DefaultHttpContext();
        if (origin is not null) http.Request.Headers.Origin = origin;
        var controller = new PersonalHistoryAuthController(service, Origins("Production", "https://app.example.test"))
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        if (allowed)
        {
            Assert.IsType<NoContentResult>(await controller.Logout(default));
            Assert.Contains("secure", Assert.Single(http.Response.Headers.SetCookie)!, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<AssistenteIaException>(() =>
                controller.Login(new("user@example.test", "senha-ficticia-123"), default));
            Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
            Assert.Equal("PERSONAL_HISTORY_ORIGIN_DENIED", exception.Code);
            Assert.Equal(0, http.Response.Headers.SetCookie.Count);
        }
    }

    private static FrontendOriginPolicy Origins(string environment, params string[] origins) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(origins.Select((origin, index) =>
                new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin))).Build(),
            new TestHostEnvironment(environment));

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/probe");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }
}
