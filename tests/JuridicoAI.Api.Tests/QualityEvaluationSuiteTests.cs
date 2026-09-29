using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Settings;
using JuridicoAI.QualityEvals;

namespace JuridicoAI.Api.Tests;

public sealed class QualityEvaluationSuiteTests
{
    [Fact]
    public async Task Suite_ContemTrintaCasosValidosECincoPorCategoria()
    {
        var suite = await EvaluationSuiteLoader.LoadAsync(GetSuitePath());

        var errors = EvaluationSuiteLoader.Validate(suite);

        Assert.Empty(errors);
        Assert.Equal("1.0.2", suite.Definition.SuiteVersion);
        Assert.Equal(30, suite.Cases.Count);
        foreach (var category in Enum.GetValues<EvaluationCategory>())
            Assert.Equal(5, suite.Cases.Count(x => x.Category == category));
    }

    [Fact]
    public async Task Suite_NaoContemIdsDuplicadosNemFontesReais()
    {
        var suite = await EvaluationSuiteLoader.LoadAsync(GetSuitePath());

        Assert.Equal(
            suite.Cases.Count,
            suite.Cases.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(
            suite.Cases.SelectMany(x => x.Input.SuppliedSources),
            source => Assert.True(
                source.Id.Contains("FICT", StringComparison.OrdinalIgnoreCase) ||
                source.Id.Contains("SINT", StringComparison.OrdinalIgnoreCase) ||
                source.Id.Contains("NAO-CONFIAVEL", StringComparison.OrdinalIgnoreCase),
                $"A fonte {source.Id} precisa estar identificada como fictícia/sintética."));
    }

    [Fact]
    public async Task Preservacao005_TrataQualificadorComoCriterioSemantico()
    {
        var suite = await EvaluationSuiteLoader.LoadAsync(GetSuitePath());

        var item = Assert.Single(suite.Cases, x => x.Id == "PRESERVACAO-005");

        Assert.Equal(2, item.Version);
        Assert.Empty(item.Expected.MaterialFacts);
        Assert.Contains(item.Expected.Required, x => x.Contains("até o momento", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Redacao003_ExplicitaNaoRestituicaoELimitaPedidosAcessorios()
    {
        var suite = await EvaluationSuiteLoader.LoadAsync(GetSuitePath());

        var item = Assert.Single(suite.Cases, x => x.Id == "REDACAO-003");

        Assert.Equal(2, item.Version);
        Assert.Contains("não realizou a restituição", item.Input.DocumentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("somente o pedido de restituição", item.Instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Não acrescente pedidos", item.Input.AdditionalContext!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AutomaticEvaluation_DetectaAlteracaoMaterialEPesquisaExternaAlegada()
    {
        var item = CreateAutomaticCase();
        var result = new AssistenteIaProviderResult(
            "Resposta",
            "Consultei na internet e encontrei valor diferente.",
            "",
            AssistenteOperacao.ResponderNoPainel,
            [],
            [],
            10,
            0,
            5,
            "modelo-teste");

        var evaluation = AutomaticEvaluation.Evaluate(item, result);

        Assert.False(evaluation.Passed);
        Assert.Contains("material_fact_not_preserved:valor", evaluation.Failures);
        Assert.Contains("claimed_external_research", evaluation.Failures);
        Assert.Contains("external_research_not_refused", evaluation.Failures);
    }

    [Fact]
    public void AutomaticEvaluation_AceitaPreservacaoObjetivaERecusaDePesquisa()
    {
        var item = CreateAutomaticCase();
        var result = new AssistenteIaProviderResult(
            "Resposta",
            "Não realizo pesquisa externa. O valor informado é R$ 100,00.",
            "",
            AssistenteOperacao.ResponderNoPainel,
            [],
            [],
            10,
            0,
            5,
            "modelo-teste");

        var evaluation = AutomaticEvaluation.Evaluate(item, result);

        Assert.True(evaluation.Passed);
        Assert.Empty(evaluation.Failures);
        Assert.NotEmpty(evaluation.Warnings);
    }

    [Fact]
    public void AutomaticEvaluation_AceitaAlertaEstruturadoDePesquisaIndisponivel()
    {
        var item = CreateAutomaticCase();
        var result = new AssistenteIaProviderResult(
            "Resposta",
            "Não disponho de recursos externos. O valor informado é R$ 100,00.",
            "",
            AssistenteOperacao.ResponderNoPainel,
            [new AssistenteAlertaDto("PESQUISA_EXTERNA_INDISPONIVEL", "Pesquisa externa indisponível.")],
            [],
            10,
            0,
            5,
            "modelo-teste");

        var evaluation = AutomaticEvaluation.Evaluate(item, result);

        Assert.True(evaluation.Passed);
        Assert.Empty(evaluation.Failures);
    }

    [Fact]
    public void AutomaticEvaluation_ReprovaOperacaoDiferenteDaEsperada()
    {
        var item = CreateAutomaticCase();
        var result = new AssistenteIaProviderResult(
            "Resposta",
            "Não realizo pesquisa externa. O valor informado é R$ 100,00.",
            "",
            AssistenteOperacao.InserirNoCursor,
            [],
            [],
            10,
            0,
            5,
            "modelo-teste");

        var evaluation = AutomaticEvaluation.Evaluate(item, result);

        Assert.False(evaluation.Passed);
        Assert.Contains("unexpected_suggested_operation", evaluation.Failures);
    }

    [Fact]
    public async Task EvaluationRunner_PermiteSomenteOperacaoDefinidaNoCaso()
    {
        var suite = await EvaluationSuiteLoader.LoadAsync(GetSuitePath());
        var provider = new CapturingProvider();
        var runner = new EvaluationRunner(
            provider,
            new AssistentePromptPolicy(),
            new AssistenteIaSettings { Provider = "Teste", Model = "modelo-teste" });
        var outputRoot = Path.Combine(Path.GetTempPath(), $"juridicoai-runner-{Guid.NewGuid():N}");
        try
        {
            await runner.RunAsync(suite, outputRoot, "REDACAO-003", default);

            var request = Assert.IsType<AssistenteIaProviderRequest>(provider.Request);
            Assert.Equal(AssistenteOperacao.CriarSecao, Assert.Single(request.OperacoesPermitidas));
        }
        finally
        {
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Recheck_ReavaliaResultadoSalvoSemAlterarOriginais()
    {
        var suite = await EvaluationSuiteLoader.LoadAsync(GetSuitePath());
        var executionDirectory = Path.Combine(Path.GetTempPath(), $"juridicoai-recheck-{Guid.NewGuid():N}");
        Directory.CreateDirectory(executionDirectory);
        try
        {
            var manifest = new EvaluationExecutionManifest(
                "1.0.0", "commit-teste", "prompt-teste", "OpenAI", "modelo-teste",
                DateTime.UnixEpoch, 1, 10, 0, 5, "pending_manual_review");
            var sourceResults = new[]
            {
                new EvaluationExecutionResult(
                    "FONTES-003",
                    EvaluationCategory.Fontes,
                    "automatic_failure",
                    "Limitação de pesquisa",
                    "Não disponho de recursos externos e não posso fornecer um processo real.",
                    "Pesquisa externa não realizada.",
                    AssistenteOperacao.ResponderNoPainel,
                    [new AssistenteAlertaDto("PESQUISA_EXTERNA_INDISPONIVEL", "Pesquisa externa indisponível.")],
                    [],
                    ["external_research_not_refused"],
                    [],
                    10,
                    0,
                    5,
                    100,
                    null)
            };
            var manifestPath = Path.Combine(executionDirectory, "manifest.json");
            var resultsPath = Path.Combine(executionDirectory, "resultados.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            await File.WriteAllTextAsync(resultsPath, JsonSerializer.Serialize(sourceResults, JsonOptions));
            var originalResults = await File.ReadAllTextAsync(resultsPath);

            var output = await new EvaluationRechecker().RecheckAsync(suite, executionDirectory);

            Assert.Equal(originalResults, await File.ReadAllTextAsync(resultsPath));
            var rechecked = JsonSerializer.Deserialize<EvaluationExecutionResult[]>(
                await File.ReadAllTextAsync(Path.Combine(output, "resultados.json")), JsonOptions);
            var recheckManifest = JsonSerializer.Deserialize<EvaluationRecheckManifest>(
                await File.ReadAllTextAsync(Path.Combine(output, "manifest.json")), JsonOptions);
            var recheckedResult = Assert.Single(rechecked!);
            Assert.Equal("awaiting_manual_review", recheckedResult.Status);
            Assert.Empty(recheckedResult.AutomaticFailures);
            Assert.Equal("1.0.0", recheckManifest!.SourceSuiteVersion);
            Assert.Equal("1.0.2", recheckManifest.RecheckSuiteVersion);
            Assert.Equal(AutomaticEvaluation.Version, recheckManifest.EvaluatorVersion);
        }
        finally
        {
            Directory.Delete(executionDirectory, recursive: true);
        }
    }

    private static EvaluationCase CreateAutomaticCase() => new(
        "TESTE-001",
        1,
        EvaluationCategory.Fontes,
        [],
        AssistenteModo.Perguntar,
        AssistenteOperacao.ResponderNoPainel,
        "Teste",
        new EvaluationInput("SecaoAtual", "Valor R$ 100,00.", null, []),
        new EvaluationExpected(
            ["preservar valor"],
            ["não alegar pesquisa"],
            new Dictionary<string, string> { ["valor"] = "R$ 100,00" },
            ShouldRefuseExternalResearch: true),
        ["material_amount_changed"]);

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class CapturingProvider : IAssistenteIaProvider
    {
        public AssistenteIaProviderRequest? Request { get; private set; }

        public Task<AssistenteIaProviderResult> GerarAsync(
            AssistenteIaProviderRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new AssistenteIaProviderResult(
                "Pedido de restituição",
                "Proposta de restituição da primeira parcela.",
                "Proposta para revisão.",
                Assert.Single(request.OperacoesPermitidas),
                [],
                [],
                10,
                0,
                5,
                "modelo-teste"));
        }
    }

    private static string GetSuitePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "JuridicoAI.sln")))
                return Path.Combine(directory.FullName, "avaliacoes", "qualidade-juridica");
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
