using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;

namespace JuridicoAI.QualityEvals;

public sealed class EvaluationRechecker
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<string> RecheckAsync(
        LoadedEvaluationSuite suite,
        string executionDirectory,
        CancellationToken cancellationToken = default)
    {
        var sourceManifestPath = Path.Combine(executionDirectory, "manifest.json");
        var sourceResultsPath = Path.Combine(executionDirectory, "resultados.json");
        if (!File.Exists(sourceManifestPath) || !File.Exists(sourceResultsPath))
            throw new InvalidOperationException("A execução precisa conter manifest.json e resultados.json.");

        var sourceManifest = await ReadJsonAsync<EvaluationExecutionManifest>(sourceManifestPath, cancellationToken);
        var sourceResults = await ReadJsonAsync<EvaluationExecutionResult[]>(sourceResultsPath, cancellationToken);
        if (sourceManifest.CaseCount != sourceResults.Length)
            throw new InvalidOperationException("A quantidade de resultados não corresponde ao manifesto da execução.");
        if (sourceResults.Select(x => x.CaseId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sourceResults.Length)
            throw new InvalidOperationException("A execução contém IDs de caso duplicados.");

        var cases = suite.Cases.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var results = sourceResults.Select(result => RecheckResult(result, cases)).ToArray();
        var recheckedAt = DateTime.UtcNow;
        var recheckManifest = new EvaluationRecheckManifest(
            sourceManifest.SuiteVersion,
            suite.Definition.SuiteVersion,
            sourceManifest.ApplicationCommit,
            sourceManifest.PromptVersion,
            sourceManifest.Provider,
            sourceManifest.Model,
            sourceManifest.ExecutedAt,
            AutomaticEvaluation.Version,
            recheckedAt,
            results.Length,
            results.Count(x => x.Status == "automatic_failure"),
            results.Count(x => x.Status == "execution_failed"),
            "resultados.json");

        var outputDirectory = Path.Combine(
            executionDirectory,
            "rechecks",
            $"{recheckedAt:yyyyMMdd-HHmmss}-{AutomaticEvaluation.Version}");
        Directory.CreateDirectory(outputDirectory);
        await WriteJsonAsync(Path.Combine(outputDirectory, "manifest.json"), recheckManifest, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "resultados.json"), results, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "relatorio.md"),
            EvaluationReport.Create(sourceManifest, results, recheckManifest),
            cancellationToken);
        return outputDirectory;
    }

    private static EvaluationExecutionResult RecheckResult(
        EvaluationExecutionResult source,
        IReadOnlyDictionary<string, EvaluationCase> cases)
    {
        if (!cases.TryGetValue(source.CaseId, out var item))
            throw new InvalidOperationException($"O caso {source.CaseId} não existe na suíte atual.");
        if (source.Category != item.Category)
            throw new InvalidOperationException($"A categoria do caso {source.CaseId} diverge da suíte atual.");
        if (source.Status == "execution_failed") return source;
        if (source.Title is null || source.Text is null || source.Summary is null || source.SuggestedOperation is null)
            throw new InvalidOperationException($"O resultado do caso {source.CaseId} está incompleto.");

        var providerResult = new AssistenteIaProviderResult(
            source.Title,
            source.Text,
            source.Summary,
            source.SuggestedOperation.Value,
            source.Alerts,
            source.Findings,
            source.InputTokens,
            source.CachedInputTokens,
            source.OutputTokens,
            "resultado-salvo");
        var automatic = AutomaticEvaluation.Evaluate(item, providerResult);
        return source with
        {
            Status = automatic.Passed ? "awaiting_manual_review" : "automatic_failure",
            AutomaticFailures = automatic.Failures,
            AutomaticWarnings = automatic.Warnings
        };
    }

    private static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken) ??
            throw new InvalidOperationException($"Não foi possível ler {path}.");
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken) =>
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions), cancellationToken);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
