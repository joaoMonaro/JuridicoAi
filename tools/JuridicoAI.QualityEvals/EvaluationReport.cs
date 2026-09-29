using System.Text;

namespace JuridicoAI.QualityEvals;

public static class EvaluationReport
{
    public static string Create(
        EvaluationExecutionManifest manifest,
        IReadOnlyList<EvaluationExecutionResult> results,
        EvaluationRecheckManifest? recheck = null)
    {
        var builder = new StringBuilder()
            .AppendLine("# Relatório de avaliação jurídica")
            .AppendLine()
            .AppendLine($"- Suíte: `{manifest.SuiteVersion}`")
            .AppendLine($"- Commit: `{manifest.ApplicationCommit}`")
            .AppendLine($"- Prompt: `{manifest.PromptVersion}`")
            .AppendLine($"- Modelo: `{manifest.Model}`")
            .AppendLine($"- Execução UTC: `{manifest.ExecutedAt:O}`");

        if (recheck is not null)
        {
            builder
                .AppendLine($"- Suíte da reavaliação: `{recheck.RecheckSuiteVersion}`")
                .AppendLine($"- Reavaliação UTC: `{recheck.RecheckedAt:O}`")
                .AppendLine($"- Avaliador: `{recheck.EvaluatorVersion}`")
                .AppendLine("- Novas chamadas ao provedor: `não`");
        }

        builder
            .AppendLine()
            .AppendLine("## Resultado automático")
            .AppendLine()
            .AppendLine("| Caso | Categoria | Estado | Falhas automáticas |")
            .AppendLine("|---|---|---|---|");
        foreach (var result in results)
            builder.AppendLine($"| {result.CaseId} | {result.Category} | {result.Status} | {string.Join(", ", result.AutomaticFailures)} |");
        builder.AppendLine().AppendLine("## Rubrica manual")
            .AppendLine()
            .AppendLine("Preencher de 0 a 2 para atendimento, correção jurídica, preservação, clareza e fontes.")
            .AppendLine("Qualquer falha impeditiva reprova a execução independentemente da média.")
            .AppendLine()
            .AppendLine("**Responsável jurídico:** pendente")
            .AppendLine().AppendLine("**Decisão:** pendente");
        return builder.ToString();
    }
}
