using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using JuridicoAI.Api.Infrastructure.Settings;

namespace JuridicoAI.Api.Tests;

public sealed partial class ProductionTransportSecurityTests
{
    [Theory]
    [InlineData("ReverseProxy:KnownProxies:0", "127.0.0.1")]
    [InlineData("ReverseProxy:KnownNetworks:0", "127.0.0.0/24")]
    public async Task Proxy_OrigemConfiavelRecuperaIpEHttpsSemAlterarHost(string key, string value)
    {
        await using var api = await TestApi.CreateAsync("Production", proxyConfiguration: new Dictionary<string, string?> { [key] = value });
        using var client = api.Client(https: false);
        using var request = ForwardedRequest("198.51.100.10");
        request.Headers.Add("X-Forwarded-Host", "host-forjado.example.test");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("198.51.100.10", Assert.Single(response.Headers.GetValues("X-Test-Remote-IP")));
        Assert.Equal("https", Assert.Single(response.Headers.GetValues("X-Test-Scheme")));
        Assert.Equal(client.BaseAddress!.Authority, Assert.Single(response.Headers.GetValues("X-Test-Host")));
        Assert.Equal(1, api.EndpointCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Proxy_ListaVaziaOuOrigemDesconhecidaNaoConverteHttpEmHttps(bool configured)
    {
        var configuration = configured
            ? new Dictionary<string, string?> { ["ReverseProxy:KnownProxies:0"] = "192.0.2.10" }
            : null;
        await using var api = await TestApi.CreateAsync("Production", proxyConfiguration: configuration);
        using var client = api.Client(https: false);
        using var request = ForwardedRequest("198.51.100.10");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, api.EndpointCalls);
    }

    [Fact]
    public async Task Proxy_RateLimitContaIpOriginalBloqueiaDecimaPrimeiraEIsolaOutroCliente()
    {
        await using var api = await TestApi.CreateAsync("Production", proxyConfiguration: TrustedLoopback());
        using var client = api.Client(https: false);

        for (var i = 0; i < 10; i++)
        {
            using var request = ForwardedRequest("198.51.100.10");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        using var excess = ForwardedRequest("198.51.100.10");
        using var blocked = await client.SendAsync(excess);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        using var other = ForwardedRequest("198.51.100.20");
        using var accepted = await client.SendAsync(other);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        using var retry = ForwardedRequest("198.51.100.10");
        using var stillBlocked = await client.SendAsync(retry);
        Assert.Equal(HttpStatusCode.TooManyRequests, stillBlocked.StatusCode);
        Assert.Equal(11, api.EndpointCalls);
    }

    [Fact]
    public async Task Proxy_ClienteDesconhecidoNaoEscapaDoRateLimitTrocandoCabecalhoDeIp()
    {
        await using var api = await TestApi.CreateAsync("Production", proxyConfiguration:
            new Dictionary<string, string?> { ["ReverseProxy:KnownProxies:0"] = "192.0.2.10" });
        using var client = api.Client(https: true);

        for (var i = 1; i <= 11; i++)
        {
            using var request = ForwardedRequest($"198.51.100.{i}");
            using var response = await client.SendAsync(request);
            Assert.Equal(i <= 10 ? HttpStatusCode.NoContent : HttpStatusCode.TooManyRequests, response.StatusCode);
            if (i <= 10)
                Assert.Equal("127.0.0.1", Assert.Single(response.Headers.GetValues("X-Test-Remote-IP")));
        }

        Assert.Equal(10, api.EndpointCalls);
    }

    [Theory]
    [InlineData("", "https")]
    [InlineData("198.51.100.10", "")]
    [InlineData("198.51.100.10, 192.0.2.10", "https")]
    public async Task Proxy_CabecalhosIncompletosOuContagensDiferentesNaoLiberamHttp(string addresses, string schemes)
    {
        await using var api = await TestApi.CreateAsync("Production", proxyConfiguration: TrustedLoopback());
        using var client = api.Client(https: false);
        using var request = ForwardedRequest(addresses, schemes);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, api.EndpointCalls);
    }

    [Theory]
    [InlineData(1, true, "192.0.2.10")]
    [InlineData(2, true, "198.51.100.10")]
    [InlineData(2, false, "192.0.2.10")]
    public async Task Proxy_CadeiaRespeitaLimiteEInterrompeAoEncontrarIntermediarioDesconhecido(
        int limit, bool trustIntermediate, string expected)
    {
        var configuration = TrustedLoopback();
        configuration["ReverseProxy:ForwardLimit"] = limit.ToString();
        if (trustIntermediate) configuration["ReverseProxy:KnownProxies:1"] = "192.0.2.10";
        await using var api = await TestApi.CreateAsync("Production", proxyConfiguration: configuration);
        using var client = api.Client(https: false);
        using var request = ForwardedRequest("198.51.100.10, 192.0.2.10", "https, https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(expected, Assert.Single(response.Headers.GetValues("X-Test-Remote-IP")));
    }

    [Fact]
    public void Proxy_ListasVaziasDesabilitamMesmoSeOpcoesAnterioresConfiavamEmTodos()
    {
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.All };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        new ReverseProxySettings().ConfigureForwardedHeaders(options);

        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Equal(1, options.ForwardLimit);
        Assert.True(options.RequireHeaderSymmetry);
    }

    [Theory]
    [InlineData("ReverseProxy:KnownProxies:0", "*")]
    [InlineData("ReverseProxy:KnownProxies:0", "proxy.example.test")]
    [InlineData("ReverseProxy:KnownProxies:0", "0.0.0.0")]
    [InlineData("ReverseProxy:KnownProxies:0", "::")]
    [InlineData("ReverseProxy:KnownNetworks:0", "10.0.0.0/99")]
    [InlineData("ReverseProxy:KnownNetworks:0", "0.0.0.0/0")]
    [InlineData("ReverseProxy:KnownNetworks:0", "::/0")]
    [InlineData("ReverseProxy:ForwardLimit", "0")]
    public async Task Proxy_ConfiguracaoInvalidaImpedeInicializacao(string key, string value)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => TestApi.CreateAsync(
            "Production", proxyConfiguration: new Dictionary<string, string?> { [key] = value }));
        Assert.Contains(key.Replace(":0", "[0]"), exception.Message);
    }

    private static Dictionary<string, string?> TrustedLoopback() =>
        new() { ["ReverseProxy:KnownProxies:0"] = "127.0.0.1" };

    private static HttpRequestMessage ForwardedRequest(string addresses, string schemes = "https")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth-probe");
        if (addresses.Length > 0) request.Headers.Add("X-Forwarded-For", addresses);
        if (schemes.Length > 0) request.Headers.Add("X-Forwarded-Proto", schemes);
        return request;
    }
}
