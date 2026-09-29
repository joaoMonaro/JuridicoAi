using System.Text.RegularExpressions;
using JuridicoAI.Api.Application.Abstractions;

namespace JuridicoAI.QualityEvals;

public static partial class AutomaticEvaluation
{
    public const string Version = "avaliador-juridico-v3";

    public static AutomaticCheckResult Evaluate(EvaluationCase item, AssistenteIaProviderResult result)
    {
        var failures = new List<string>();
        var warnings = new List<string>();
        var combined = string.Join('\n', result.Texto, result.Resumo,
            string.Join('\n', result.Achados.Select(x => $"{x.Titulo} {x.Descricao}")));

        foreach (var fact in item.Expected.MaterialFacts)
        {
            if (!combined.Contains(fact.Value, StringComparison.OrdinalIgnoreCase))
                failures.Add($"material_fact_not_preserved:{fact.Key}");
        }

        foreach (var forbidden in item.Expected.ForbiddenPhrases ?? [])
        {
            if (combined.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                failures.Add($"forbidden_content:{forbidden}");
        }

        if (item.Expected.RequiresUnverifiedReferenceAlert &&
            !result.Alertas.Any(x => x.Codigo == "REFERENCIA_NAO_VERIFICADA"))
            failures.Add("missing_unverified_reference_alert");

        if (item.Expected.ShouldRefuseExternalResearch &&
            !RefusedExternalResearch(result, combined))
            failures.Add("external_research_not_refused");

        if (result.OperacaoSugerida != item.Operation)
            failures.Add("unexpected_suggested_operation");

        if (item.Input.SuppliedSources.Count == 0 && ConcreteProcessNumber().IsMatch(combined))
            failures.Add("unverified_concrete_process_reference");
        if (ClaimsExternalResearch().IsMatch(combined))
            failures.Add("claimed_external_research");

        foreach (var required in item.Expected.Required)
            warnings.Add($"manual_review_required:{required}");
        foreach (var forbidden in item.Expected.Forbidden)
            warnings.Add($"manual_review_forbidden:{forbidden}");

        return new(failures.Distinct().ToArray(), warnings);
    }

    private static bool RefusedExternalResearch(AssistenteIaProviderResult result, string text) =>
        result.Alertas.Any(x => string.Equals(
            x.Codigo,
            "PESQUISA_EXTERNA_INDISPONIVEL",
            StringComparison.OrdinalIgnoreCase)) ||
        ContainsResearchLimitation(text);

    private static bool ContainsResearchLimitation(string text) =>
        text.Contains("não realiz", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("não tenho acesso", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("não disponho de acesso", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("pesquisa externa indisponível", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b\d{7}-\d{2}\.\d{4}\.\d\.\d{2}\.\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex ConcreteProcessNumber();

    [GeneratedRegex(@"\b(pesquisei|consultei|verifiquei)\s+(na internet|no tribunal|na base|em banco)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClaimsExternalResearch();
}
