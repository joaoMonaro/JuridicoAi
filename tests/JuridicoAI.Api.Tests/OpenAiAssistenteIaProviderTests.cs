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
    [Theory]
    [InlineData("""{"titulo":"Resposta","texto":"Texto","resumo":"","intencao":"Responder","alertas":[],"grupos":[]}""")]
    [InlineData("""{"titulo":"Resposta","texto":"Texto","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false,"aplicarAgora":true}""")]
    public async Task ConversaLivre_RecusaResultadoIncompletoOuComComandoExtra(string output)
    {
        var delta = JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = output });
        var handler = new StubHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent($"data: {delta}\n\ndata: {{\"type\":\"response.completed\"}}\n\n", Encoding.UTF8, "text/event-stream") });
        var error = await Assert.ThrowsAsync<AssistenteIaProviderException>(() => CreateProvider(handler).GerarAsync(
            new("Política", "Contexto", AssistenteModo.Perguntar, [AssistenteOperacao.ResponderNoPainel], 1000, true), default));
        Assert.Equal("AI_PROVIDER_ERROR", error.Code);
    }

    [Fact]
    public async Task ConversaLivre_UsaSchemaFechadoComIntencaoEGruposTipados()
    {
        var text = """{"titulo":"Minuta","texto":"Preparei a minuta para revisão.","resumo":"","intencao":"GerarMinuta","alertas":[],"grupos":[{"explicacao":"Criar minuta","operacoes":[{"startIndex":0,"deleteCount":1,"novosBlocos":[{"type":"paragraph","attrs":{"textAlign":null},"content":[{"type":"text","text":"Corpo jurídico","marks":[]}]}]}]}],"substituirPropostaReferenciada":false}""";
        var delta = JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = text });
        var handler = new StubHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent($"data: {delta}\n\ndata: {{\"type\":\"response.completed\"}}\n\n", Encoding.UTF8, "text/event-stream") });
        var result = await CreateProvider(handler).GerarAsync(new("Política", "Contexto", AssistenteModo.Perguntar,
            [AssistenteOperacao.ResponderNoPainel, AssistenteOperacao.ProporAlteracoes], 1000, true, true), default);
        Assert.Equal(AssistenteOperacao.ProporAlteracoes, result.OperacaoSugerida);
        Assert.Equal(AssistenteIntencao.GerarMinuta, result.RascunhoAutomatico!.Intencao);
        Assert.Single(result.RascunhoAutomatico.Grupos);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        var format = body.RootElement.GetProperty("text").GetProperty("format");
        Assert.Equal("assistente_conversacional_response", format.GetProperty("name").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
        var schema = format.GetProperty("schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.True(schema.GetProperty("$defs").TryGetProperty("Node", out _));
        Assert.Contains("GerarMinuta", schema.GetProperty("properties").GetProperty("intencao").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task ConversaLivre_CatalogoDeChatNaoAnunciaOperacoesDeEdicao()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":10}") });
        await CreateProvider(handler).ContarTokensEntradaAsync(new("Política", "Contexto", AssistenteModo.Perguntar,
            [AssistenteOperacao.ResponderNoPainel], 1000, true, false), default);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        var values = body.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema").GetProperty("properties").GetProperty("intencao").GetProperty("enum");
        Assert.Equal("Responder", Assert.Single(values.EnumerateArray()).GetString());
    }

    [Fact]
    public async Task ContarTokensEntrada_UsaEndpointOficialComOMesmoInput()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"object":"response.input_tokens","input_tokens":1234}""")
        });
        var provider = CreateProvider(handler);
        var request = new AssistenteIaProviderRequest(
            "Instruções seguras",
            "Contexto jurídico",
            AssistenteModo.Perguntar,
            [AssistenteOperacao.ResponderNoPainel],
            1_000);

        var result = await provider.ContarTokensEntradaAsync(request, default);

        Assert.Equal(1234, result);
        Assert.Equal("/v1/responses/input_tokens", handler.RequestPath);
        Assert.NotNull(handler.RequestBody);
        using var body = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("modelo-teste", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("Instruções seguras", body.RootElement.GetProperty("instructions").GetString());
        Assert.Equal("Contexto jurídico", body.RootElement.GetProperty("input").GetString());
        Assert.Equal("json_schema", body.RootElement.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.False(body.RootElement.TryGetProperty("store", out _));
        Assert.False(body.RootElement.TryGetProperty("stream", out _));
    }

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
        public string? RequestPath { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            RequestPath = request.RequestUri?.AbsolutePath;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
