using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
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
