using System.Net;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;

namespace JuridicoAI.Api.Tests;

public sealed partial class OpenAiDocumentRetrievalTests
{
    private static AssistenteDocumentTools MemoryTools(Func<string, string, CancellationToken, Task<string>> execute, Func<string?> required) =>
        new(AssistenteHistoryService.Definitions, execute, () => new("Completa", "Relevantes", []), 2, 10000, 80000)
        { HasDocumentTools = false, RequiredTool = required, InitialInputTokens = 10 };

    [Fact]
    public async Task Analise_SinteseConcluiAposUmaChamadaDeFerramentaSemGeracaoAdicional()
    {
        var generated = 0; var executed = 0;
        var tools = new AssistenteDocumentTools(JuridicoAI.Api.Infrastructure.Documents.AssistenteDocumentAnalysisProcessor.SummaryDefinition,
            (_, _, _) => { executed++; return Task.FromResult("Síntese validada."); }, () => new("Completa", "Relevantes", []), 1, 10000, 80000)
        {
            HasDocumentTools = false, RequiredTool = () => "registrar_sintese_documental", InitialInputTokens = 100,
            CompleteAfterTool = () => new("Síntese", "Validada.", "", AssistenteOperacao.ResponderNoPainel, [], [], null, null, null, "fixture", new(AssistenteIntencao.Responder, [], false))
        };
        var provider = Provider(new Handler(_ => { generated++; return Task.FromResult(Stream(null,
            [new { type = "function_call", name = "registrar_sintese_documental", call_id = "summary-1", arguments = "{}" }])); }));
        var result = await provider.GerarAsync(Request(tools), default);
        Assert.Equal(1, generated); Assert.Equal(1, executed); Assert.Equal(10, result.EntradaTokens); Assert.Equal(5, result.SaidaTokens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Memoria_FornecedorNaoPodeIgnorarFerramentaObrigatoria(bool wrongFunction)
    {
        var executed = 0;
        var tools = MemoryTools((_, _, _) => { executed++; return Task.FromResult("{}"); }, () => "atualizar_memoria");
        var provider = Provider(new Handler(_ => Task.FromResult(wrongFunction ? Stream(null, [Function("wrong")]) :
            Stream(Final.Replace(",\"coberturaPretendida\":\"Relevantes\"", ""), []))));
        await Assert.ThrowsAsync<AssistenteIaProviderException>(() => provider.GerarAsync(Request(tools), default));
        Assert.Equal(0, executed);
    }

    [Fact]
    public async Task Memoria_ContagemInicialUsaMesmoToolChoiceESchemaSemEscopoDocumental()
    {
        var tools = MemoryTools((_, _, _) => Task.FromResult("{}"), () => "atualizar_memoria");
        var provider = Provider(new Handler(async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("atualizar_memoria", body.RootElement.GetProperty("tool_choice").GetProperty("name").GetString());
            var schema = body.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema");
            Assert.False(schema.GetProperty("properties").TryGetProperty("coberturaPretendida", out _));
            Assert.False(body.RootElement.TryGetProperty("include", out _));
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":123}") };
        }));
        Assert.Equal(123, await provider.ContarTokensEntradaAsync(Request(tools), default));
    }

    [Fact]
    public async Task Memoria_LimiteAntesDaCompactacaoNaoProduzPropostaNemFingeTerConsultadoHistorico()
    {
        var tools = MemoryTools((_, _, _) => throw new InvalidOperationException(), () => "atualizar_memoria");
        tools.InitialInputTokens = 10001;
        var result = await Provider(new Handler(_ => throw new InvalidOperationException())).GerarAsync(Request(tools), default);
        Assert.Empty(result.RascunhoAutomatico!.Grupos); Assert.Null(result.CoberturaPretendida);
        Assert.Contains(result.Alertas, a => a.Codigo == "HISTORY_RETRIEVAL_LIMIT");
        Assert.Contains("histórico permanece salvo", result.Texto);
    }
}
