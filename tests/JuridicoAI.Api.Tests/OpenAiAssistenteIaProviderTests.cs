using System.Net;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class OpenAiAssistenteIaProviderTests
{
    [Fact]
    public async Task Gerar_UsaResponsesSemRetencaoELeStreamEstruturado()
    {
        var structured = JsonSerializer.Serialize(new
        {
            titulo = "Análise",
            texto = "Resposta da IA",
            resumo = "Resumo",
            operacaoSugerida = "ResponderNoPainel",
            alertas = Array.Empty<object>(),
            achados = Array.Empty<object>()
        });
        var delta = JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = structured });
        var completed = JsonSerializer.Serialize(new
        {
            type = "response.completed",
            response = new
            {
                model = "modelo-resolvido",
                usage = new
                {
                    input_tokens = 42,
                    input_tokens_details = new { cached_tokens = 12 },
                    output_tokens = 17
                }
            }
        });
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"data: {delta}\n\ndata: {completed}\n\n", Encoding.UTF8, "text/event-stream")
        });
        var provider = CreateProvider(handler);
        var request = new AssistenteIaProviderRequest(
            "Instruções seguras",
            "Contexto",
            AssistenteModo.Perguntar,
            [AssistenteOperacao.ResponderNoPainel],
            1_000);

        var result = await provider.GerarAsync(request, default);

        Assert.Equal("Resposta da IA", result.Texto);
        Assert.Equal(AssistenteOperacao.ResponderNoPainel, result.OperacaoSugerida);
        Assert.Equal(42, result.EntradaTokens);
        Assert.Equal(12, result.EntradaTokensEmCache);
        Assert.Equal(17, result.SaidaTokens);
        Assert.Equal("modelo-resolvido", result.Modelo);
        Assert.NotNull(handler.RequestBody);
        using var body = JsonDocument.Parse(handler.RequestBody);
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("json_schema", body.RootElement.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
    }

    [Fact]
    public async Task Gerar_Converte429EmErroDeLimiteSemExporResposta()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("detalhe sensível do provedor")
        });
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AssistenteIaProviderException>(() => provider.GerarAsync(
            new AssistenteIaProviderRequest(
                "Instruções",
                "Contexto",
                AssistenteModo.Perguntar,
                [AssistenteOperacao.ResponderNoPainel],
                1_000),
            default));

        Assert.Equal("AI_RATE_LIMITED", exception.Code);
        Assert.True(exception.Retryable);
        Assert.DoesNotContain("sensível", exception.Message);
    }

    private static OpenAiAssistenteIaProvider CreateProvider(StubHandler handler)
    {
        var settings = new AssistenteIaSettings
        {
            Enabled = true,
            Model = "modelo-teste",
            ApiKey = "segredo-teste"
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri(settings.BaseUrl) };
        return new OpenAiAssistenteIaProvider(client, Options.Create(settings));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public string? AuthorizationScheme { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
