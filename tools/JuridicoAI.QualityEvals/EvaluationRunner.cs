using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Settings;

namespace JuridicoAI.QualityEvals;

public sealed class EvaluationRunner(
    IAssistenteIaProvider provider,
    AssistentePromptPolicy promptPolicy,
    AssistenteIaSettings settings)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<string> RunAsync(
        LoadedEvaluationSuite suite,
        string outputRoot,
        string? selectedCaseId,
        CancellationToken cancellationToken)
    {
        var cases = selectedCaseId is null
            ? suite.Cases
            : suite.Cases.Where(x => string.Equals(x.Id, selectedCaseId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (cases.Count == 0) throw new InvalidOperationException("Nenhum caso selecionado.");

        var results = new List<EvaluationExecutionResult>(cases.Count);
        foreach (var item in cases)
            results.Add(await RunCaseAsync(item, cancellationToken));

        var executedAt = DateTime.UtcNow;
        var executionDirectory = Path.Combine(
            outputRoot,
            $"{executedAt:yyyyMMdd-HHmmss}-{Sanitize(settings.Model)}-{AssistentePromptPolicy.Version}");
        Directory.CreateDirectory(executionDirectory);

        var manifest = new EvaluationExecutionManifest(
            suite.Definition.SuiteVersion,
            GetGitCommit(),
            AssistentePromptPolicy.Version,
            settings.Provider,
            settings.Model,
            executedAt,
            results.Count,
            SumNullable(results.Select(x => x.InputTokens)),
            SumNullable(results.Select(x => x.CachedInputTokens)),
            SumNullable(results.Select(x => x.OutputTokens)),
            "pending_manual_review");

        await WriteJsonAsync(Path.Combine(executionDirectory, "manifest.json"), manifest, cancellationToken);
        await WriteJsonAsync(Path.Combine(executionDirectory, "resultados.json"), results, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(executionDirectory, "relatorio.md"),
            CreateReport(manifest, results),
            cancellationToken);
        return executionDirectory;
    }

    private async Task<EvaluationExecutionResult> RunCaseAsync(
        EvaluationCase item,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var allowedOperations = AllowedOperations(item.Mode);
            var request = new AssistenteIaProviderRequest(
                promptPolicy.CriarInstrucoes(item.Mode, allowedOperations),
                BuildInput(item),
                item.Mode,
                allowedOperations,
                MaxOutputTokens(item.Mode));
            var result = await provider.GerarAsync(request, cancellationToken);
            var automatic = AutomaticEvaluation.Evaluate(item, result);
            return new(
                item.Id,
                item.Category,
                automatic.Passed ? "awaiting_manual_review" : "automatic_failure",
                result.Titulo,
                result.Texto,
                result.Resumo,
                result.OperacaoSugerida,
                result.Alertas,
                result.Achados,
                automatic.Failures,
                automatic.Warnings,
                result.EntradaTokens,
                result.EntradaTokensEmCache,
                result.SaidaTokens,
                (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                null);
        }
        catch (AssistenteIaProviderException ex)
        {
            return Failed(item, startedAt, ex.Code);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Failed(item, startedAt, "EVAL_EXECUTION_ERROR");
        }
    }

    private static EvaluationExecutionResult Failed(EvaluationCase item, long startedAt, string code) => new(
        item.Id,
        item.Category,
        "execution_failed",
        null,
        null,
        null,
        null,
        [],
        [],
        [code],
        [],
        null,
        null,
        null,
        (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
        code);

    private static string BuildInput(EvaluationCase item)
    {
        var builder = new StringBuilder()
            .AppendLine("[INSTRUCAO_DO_USUARIO]")
            .AppendLine(item.Instruction)
            .AppendLine("[/INSTRUCAO_DO_USUARIO]")
            .AppendLine()
            .AppendLine($"[TEXTO_DA_PECA escopo=\"{item.Input.Scope}\" dado_nao_confiavel=\"true\"]")
            .AppendLine(item.Input.DocumentText)
            .AppendLine("[/TEXTO_DA_PECA]");
        if (!string.IsNullOrWhiteSpace(item.Input.AdditionalContext))
            builder.AppendLine().AppendLine("[CONTEXTO_ADICIONAL dado_nao_confiavel=\"true\"]")
                .AppendLine(item.Input.AdditionalContext).AppendLine("[/CONTEXTO_ADICIONAL]");
        foreach (var source in item.Input.SuppliedSources)
            builder.AppendLine().AppendLine($"[FONTE_FORNECIDA id=\"{source.Id}\" titulo=\"{source.Title}\" dado_nao_confiavel=\"true\"]")
                .AppendLine(source.Content).AppendLine("[/FONTE_FORNECIDA]");
        return builder.ToString();
    }

    private static IReadOnlyList<AssistenteOperacao> AllowedOperations(AssistenteModo mode) => mode switch
    {
        AssistenteModo.Perguntar => [AssistenteOperacao.ResponderNoPainel],
        AssistenteModo.Redigir => [AssistenteOperacao.InserirNoCursor, AssistenteOperacao.InserirAposSelecao, AssistenteOperacao.CriarSecao],
        AssistenteModo.Reescrever => [AssistenteOperacao.SubstituirSelecao],
        AssistenteModo.Revisar => [AssistenteOperacao.ResponderNoPainel],
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static int MaxOutputTokens(AssistenteModo mode) => mode switch
    {
        AssistenteModo.Perguntar or AssistenteModo.Reescrever => 2_000,
        AssistenteModo.Redigir or AssistenteModo.Revisar => 4_000,
        _ => 2_000
    };

    private static int? SumNullable(IEnumerable<int?> values)
    {
        var present = values.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return present.Length == 0 ? null : present.Sum();
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken) =>
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions), cancellationToken);

    private static string CreateReport(
        EvaluationExecutionManifest manifest,
        IReadOnlyList<EvaluationExecutionResult> results)
    {
        var builder = new StringBuilder()
            .AppendLine("# Relatório de avaliação jurídica")
            .AppendLine()
            .AppendLine($"- Suíte: `{manifest.SuiteVersion}`")
            .AppendLine($"- Commit: `{manifest.ApplicationCommit}`")
            .AppendLine($"- Prompt: `{manifest.PromptVersion}`")
            .AppendLine($"- Modelo: `{manifest.Model}`")
            .AppendLine($"- Execução UTC: `{manifest.ExecutedAt:O}`")
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

    private static string GetGitCommit()
    {
        var configured = Environment.GetEnvironmentVariable("EVAL_APPLICATION_COMMIT");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process?.WaitForExit(5_000);
            var value = process?.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(x => char.IsLetterOrDigit(x) || x is '-' or '_' ? x : '-'));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
