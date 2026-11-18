using System.Net;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class AssistenteLatencyTests
{
    [Fact]
    public async Task Provedor_SeparaContagemCabecalhosEGeracaoSemRegistrarConteudoOuCredenciais()
    {
        var log = new Capture<OpenAiAssistenteIaProvider>();
        var payload = """{"titulo":"Resposta","texto":"PRIVATE_RESPONSE","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false}""";
        var delta = JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = payload });
        var provider = new OpenAiAssistenteIaProvider(new HttpClient(new Handler(message =>
            message.RequestUri!.AbsolutePath.EndsWith("input_tokens")
                ? new(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":42}") }
                : new(HttpStatusCode.OK) { Content = new StringContent($"data: {delta}\n\ndata: {{\"type\":\"response.completed\"}}\n\n", Encoding.UTF8, "text/event-stream") }))
            { BaseAddress = new("https://synthetic.test/v1/") },
            Options.Create(new AssistenteIaSettings { Model = "fixture", ApiKey = "PRIVATE_CREDENTIAL" }), log);
        var request = new AssistenteIaProviderRequest("PRIVATE_POLICY", "PRIVATE_DOCUMENT", AssistenteModo.Perguntar,
            [AssistenteOperacao.ResponderNoPainel], 1000, true);
        Assert.Equal(42, await provider.ContarTokensEntradaAsync(request, default));
        var result = await provider.GerarAsync(request, default);
        Assert.Equal("PRIVATE_RESPONSE", result.Texto);
        Assert.Equal(new[] { "provider.input_tokens", "provider.headers", "provider.generation" },
            log.Events.Select(e => (string)e["Stage"]!));
        Assert.All(log.Events, e => Assert.True(Convert.ToDouble(e["DurationMs"]) >= 0));
        var serialized = JsonSerializer.Serialize(log.Events);
        Assert.DoesNotContain("PRIVATE_", serialized);
    }

    [Fact]
    public async Task Medicao_FalhaMantemDuracaoSemRegistrarMensagemDaExcecao()
    {
        var log = new Capture<AssistenteLatencyTests>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => AssistenteLatency.MeasureAsync<int>(log,
            "generation.context", () => throw new InvalidOperationException("PRIVATE_FAILURE_CONTENT")));
        Assert.Single(log.Events);
        Assert.Equal("generation.context", log.Events[0]["Stage"]);
        Assert.DoesNotContain("PRIVATE_FAILURE_CONTENT", JsonSerializer.Serialize(log.Events));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }
    private sealed class Capture<T> : ILogger<T>
    {
        public List<Dictionary<string, object?>> Events { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        {
            Assert.Null(error);
            Assert.Equal(AssistenteLatency.StageEventId, id.Id);
            Events.Add(((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(p => p.Key, p => p.Value));
        }
    }
}
