using System.Net;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class OpenAiDocumentRetrievalTests
{
    private const string Final = """{"titulo":"Resposta","texto":"Resposta com fonte.","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false,"coberturaPretendida":"Relevantes"}""";
    [Fact]
    public async Task Ferramentas_ReenviaOutputCanonicoEContabilizaCadaChamadaSemEstadoNoProvedor()
    {
        var generated = new List<JsonElement>(); var counted = new List<JsonElement>(); var executed = 0;
        var calls = new List<AssistenteProviderCall>();
        var tools = Tools((_, _, _) => { executed++; return Task.FromResult("{\"texto\":\"trecho confiável\"}"); });
        tools.RecordCall = call => { calls.Add(call); return Task.CompletedTask; };
        tools.InitialInputTokens = 100;
        var handler = new Handler(async req =>
        {
            var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement.Clone();
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
            { counted.Add(body); return new(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":120}") }; }
            generated.Add(body);
            return generated.Count == 1 ? Stream(null, [new { type = "reasoning", id = "rs_fixture", encrypted_content = "opaque-encrypted", summary = Array.Empty<object>() },
                Function("call_1")]) : Stream(Final, []);
        });
        var result = await Provider(handler).GerarAsync(Request(tools), default);
        Assert.Equal(1, executed); Assert.Equal(2, generated.Count); Assert.Single(counted); Assert.Equal(2, calls.Count);
        Assert.Equal(20, result.EntradaTokens); Assert.Equal(10, result.SaidaTokens);
        var replay = generated[1].GetProperty("input").EnumerateArray().ToArray();
        Assert.Equal("opaque-encrypted", replay.Single(x => x.TryGetProperty("type", out var type) && type.GetString() == "reasoning").GetProperty("encrypted_content").GetString());
        Assert.Equal("call_1", replay.Single(x => x.TryGetProperty("type", out var t) && t.GetString() == "function_call_output").GetProperty("call_id").GetString());
        Assert.All(generated, payload => { Assert.False(payload.GetProperty("store").GetBoolean()); Assert.False(payload.GetProperty("parallel_tool_calls").GetBoolean()); Assert.False(payload.TryGetProperty("previous_response_id", out _)); });
        Assert.False(counted[0].TryGetProperty("include", out _));
        var schema = generated[0].GetProperty("text").GetProperty("format").GetProperty("schema");
        Assert.True(schema.GetProperty("properties").TryGetProperty("coberturaPretendida", out _));
        Assert.All(generated[0].GetProperty("tools").EnumerateArray(), tool =>
        { Assert.True(tool.GetProperty("strict").GetBoolean()); Assert.False(tool.GetProperty("parameters").GetProperty("additionalProperties").GetBoolean()); });
    }
    [Fact]
    public async Task Ferramentas_BudgetAntesDaChamadaPreservaCoberturaSemGerarProposta()
    {
        var requests = 0; var limited = false;
        var tools = Tools((_, _, _) => throw new InvalidOperationException(), maxTokens: 1000);
        tools.InitialInputTokens = 10; tools.OnLimit = _ => { limited = true; return Task.CompletedTask; };
        var result = await Provider(new Handler(_ => { requests++; throw new InvalidOperationException(); })).GerarAsync(Request(tools), default);
        Assert.True(limited); Assert.Equal(0, requests); Assert.Equal(AssistenteOperacao.ResponderNoPainel, result.OperacaoSugerida);
        Assert.Empty(result.RascunhoAutomatico!.Grupos);
    }
    [Theory]
    [InlineData("in_progress", 1)]
    [InlineData("completed", 2)]
    public async Task Ferramentas_NaoExecutaChamadaIncompletaOuParalela(string status, int count)
    {
        var executed = 0; var tools = Tools((_, _, _) => { executed++; return Task.FromResult("{}"); }); tools.InitialInputTokens = 1;
        var functions = Enumerable.Range(0, count).Select(i => (object)new { type = "function_call", status, call_id = "call_" + i,
            name = "buscar_fontes", arguments = "{\"consulta\":\"fato\",\"limite\":1}" }).ToArray();
        await Assert.ThrowsAsync<AssistenteIaProviderException>(() => Provider(new Handler(_ => Task.FromResult(Stream(null, functions)))).GerarAsync(Request(tools), default));
        Assert.Equal(0, executed);
    }
    [Fact]
    public async Task Ferramentas_UltimaRodadaDesabilitaNovasConsultas()
    {
        var generated = 0; var executed = 0;
        var tools = Tools((_, _, _) => { executed++; return Task.FromResult("{}"); }, maxRounds: 1); tools.InitialInputTokens = 1;
        var handler = new Handler(async req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens")) return new(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":1}") };
            using var json = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
            generated++;
            if (generated == 2) Assert.Equal("none", json.RootElement.GetProperty("tool_choice").GetString());
            return Stream(null, [Function("call_" + generated)]);
        });
        await Assert.ThrowsAsync<AssistenteIaProviderException>(() => Provider(handler).GerarAsync(Request(tools), default));
        Assert.Equal(2, generated); Assert.Equal(1, executed);
    }
    [Fact]
    public void Cobertura_TodosIncompletaNuncaRetornaAlteracoesAplicaveis()
    {
        var draft = new AssistenteIaProviderResult("Minuta", "Texto", "", AssistenteOperacao.ProporAlteracoes, [], [], 1, 0, 1, "fixture",
            new(AssistenteIntencao.GerarMinuta, [new("Alterar", [new(0, 1, [])])], false), "Todos");
        var result = AssistenteDocumentRetrievalPolicy.ValidateCoverage(draft, Coverage("Todos"));
        Assert.Equal(AssistenteOperacao.ResponderNoPainel, result.OperacaoSugerida);
        Assert.Empty(result.RascunhoAutomatico!.Grupos); Assert.Contains(result.Alertas, a => a.Codigo == "DOCUMENT_COVERAGE_INCOMPLETE");
    }
    [Fact]
    public async Task Ferramentas_RecontaEntradaEBloqueiaAntesDeUmaSegundaGeracaoAcimaDoLimite()
    {
        var generated = 0; var counted = 0;
        var tools = Tools((_, _, _) => Task.FromResult("{}")); tools.InitialInputTokens = 10; tools.MaxInputTokens = 50;
        var handler = new Handler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("input_tokens"))
            { counted++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":100}") }); }
            generated++; return Task.FromResult(Stream(null, [Function("call_one")]));
        });
        var result = await Provider(handler).GerarAsync(Request(tools), default);
        Assert.Equal(1, generated); Assert.Equal(1, counted);
        Assert.Empty(result.RascunhoAutomatico!.Grupos); Assert.Contains(result.Alertas, a => a.Codigo == "DOCUMENT_RETRIEVAL_LIMIT");
    }
    [Fact]
    public async Task Ferramentas_RevogacaoAntesDaRodadaSeguinteImpedeNovoEnvioAoProvedor()
    {
        var generated = 0; var checks = 0;
        var tools = Tools((_, _, _) => Task.FromResult("{}")); tools.InitialInputTokens = 10;
        tools.BeforeCall = _ => ++checks == 3 ? Task.FromException(new JuridicoAI.Api.Application.Exceptions.AssistenteIaException(403, "AI_FEATURE_NOT_ENABLED", "Revogado")) : Task.CompletedTask;
        var handler = new Handler(_ => { generated++; return Task.FromResult(Stream(null, [Function("call_one")])); });
        await Assert.ThrowsAsync<JuridicoAI.Api.Application.Exceptions.AssistenteIaException>(() => Provider(handler).GerarAsync(Request(tools), default));
        Assert.Equal(1, generated);
    }
    [Fact]
    public void Cobertura_RelevantesPermiteRespostaMasSinalizaTrechosNaoConsultados()
    {
        var result = AssistenteDocumentRetrievalPolicy.ValidateCoverage(new("Resposta", "Texto", "", AssistenteOperacao.ResponderNoPainel,
            [], [], 1, 0, 1, "fixture", new(AssistenteIntencao.Responder, [], false), "Relevantes"), Coverage());
        Assert.Contains(result.Alertas, a => a.Codigo == "DOCUMENT_RELEVANT_EXCERPTS_ONLY");
    }
    private static AssistenteCoberturaDocumentalDto Coverage(string scope = "Relevantes") => new("Parcial", scope,
        [new(Guid.Empty, "fonte.txt", "AnexoPrivado", 10, 0, 0, "Concluido")]);
    private static AssistenteDocumentTools Tools(Func<string, string, CancellationToken, Task<string>> execute, int maxTokens = 180000, int maxRounds = 8) =>
        new(AssistenteDocumentToolSession.Definitions, execute, () => Coverage(), maxRounds, maxTokens, 80000);
    private static AssistenteIaProviderRequest Request(AssistenteDocumentTools tools) => new("Política", "Pedido e catálogo", AssistenteModo.Perguntar,
        [AssistenteOperacao.ResponderNoPainel], 1000, true, false, tools);
    private static object Function(string id) => new { type = "function_call", call_id = id, name = "buscar_fontes", arguments = "{\"consulta\":\"fato\",\"limite\":1}" };
    private static HttpResponseMessage Stream(string? text, object[] output)
    {
        var delta = text is null ? "" : "data: " + JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = text }) + "\n\n";
        var completed = JsonSerializer.Serialize(new { type = "response.completed", response = new { status = "completed", model = "fixture",
            output, usage = new { input_tokens = 10, output_tokens = 5, input_tokens_details = new { cached_tokens = 0 } } } });
        return new(HttpStatusCode.OK) { Content = new StringContent(delta + "data: " + completed + "\n\n", Encoding.UTF8, "text/event-stream") };
    }
    private static OpenAiAssistenteIaProvider Provider(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new("https://provider.test/v1/") },
        Options.Create(new AssistenteIaSettings { Model = "fixture", ApiKey = "synthetic" }));
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request); }
}
