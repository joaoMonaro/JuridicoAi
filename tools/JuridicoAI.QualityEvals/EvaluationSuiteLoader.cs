using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.DTOs;

namespace JuridicoAI.QualityEvals;

public static class EvaluationSuiteLoader
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task<LoadedEvaluationSuite> LoadAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        var definitionPath = Path.Combine(rootPath, "suite.json");
        if (!File.Exists(definitionPath))
            throw new InvalidOperationException($"Arquivo da suíte não encontrado: {definitionPath}");

        await using var definitionStream = File.OpenRead(definitionPath);
        var definition = await JsonSerializer.DeserializeAsync<EvaluationSuiteDefinition>(
            definitionStream,
            JsonOptions,
            cancellationToken) ?? throw new InvalidOperationException("suite.json inválido.");

        var cases = new List<EvaluationCase>();
        foreach (var relativePath in definition.CaseFiles)
        {
            var casePath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
            if (!casePath.StartsWith(Path.GetFullPath(rootPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Caminho de casos fora da raiz da suíte.");
            if (!File.Exists(casePath))
                throw new InvalidOperationException($"Arquivo de casos não encontrado: {relativePath}");

            await using var caseStream = File.OpenRead(casePath);
            var fileCases = await JsonSerializer.DeserializeAsync<IReadOnlyList<EvaluationCase>>(
                caseStream,
                JsonOptions,
                cancellationToken) ?? throw new InvalidOperationException($"Arquivo inválido: {relativePath}");
            cases.AddRange(fileCases);
        }

        return new LoadedEvaluationSuite(Path.GetFullPath(rootPath), definition, cases);
    }

    public static IReadOnlyList<string> Validate(LoadedEvaluationSuite suite)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(suite.Definition.SuiteVersion))
            errors.Add("suiteVersion é obrigatório.");
        if (suite.Cases.Count != suite.Definition.ExpectedCaseCount)
            errors.Add($"Esperados {suite.Definition.ExpectedCaseCount} casos; encontrados {suite.Cases.Count}.");

        if (suite.Definition.ExpectedCategoryCounts is { Count: > 0 } categoryCounts)
        {
            if (categoryCounts.Values.Sum() != suite.Definition.ExpectedCaseCount)
                errors.Add("A soma de expectedCategoryCounts deve corresponder a expectedCaseCount.");
            foreach (var category in Enum.GetValues<EvaluationCategory>())
            {
                var expected = categoryCounts.GetValueOrDefault(category);
                var count = suite.Cases.Count(x => x.Category == category);
                if (count != expected)
                    errors.Add($"Categoria {category}: esperados {expected}; encontrados {count}.");
            }
        }
        else if (suite.Definition.ExpectedCasesPerCategory is { } expectedPerCategory)
        {
            foreach (var category in Enum.GetValues<EvaluationCategory>())
            {
                var count = suite.Cases.Count(x => x.Category == category);
                if (count != expectedPerCategory)
                    errors.Add($"Categoria {category}: esperados {expectedPerCategory}; encontrados {count}.");
            }
        }
        else
            errors.Add("Defina expectedCasesPerCategory ou expectedCategoryCounts.");

        foreach (var duplicate in suite.Cases.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            errors.Add($"ID duplicado: {duplicate.Key}.");

        foreach (var item in suite.Cases)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || item.Version <= 0)
                errors.Add("Todo caso precisa de ID e versão positiva.");
            if (string.IsNullOrWhiteSpace(item.Instruction) || string.IsNullOrWhiteSpace(item.Input.DocumentText))
                errors.Add($"{item.Id}: instrução e texto do documento são obrigatórios.");
            if (item.Expected.Required.Count == 0 || item.Expected.Forbidden.Count == 0)
                errors.Add($"{item.Id}: expected.required e expected.forbidden não podem ser vazios.");
            if (item.AutomaticFailures.Count == 0)
                errors.Add($"{item.Id}: ao menos uma falha automática deve ser declarada.");
            if (!IsOperationAllowed(item.Mode, item.Operation))
                errors.Add($"{item.Id}: operação {item.Operation} incompatível com modo {item.Mode}.");
        }

        return errors;
    }

    private static bool IsOperationAllowed(AssistenteModo mode, AssistenteOperacao operation) => mode switch
    {
        AssistenteModo.Perguntar => operation == AssistenteOperacao.ResponderNoPainel,
        AssistenteModo.Redigir => operation is AssistenteOperacao.InserirNoCursor or
            AssistenteOperacao.InserirAposSelecao or AssistenteOperacao.CriarSecao,
        AssistenteModo.Reescrever => operation == AssistenteOperacao.SubstituirSelecao,
        AssistenteModo.Revisar => operation == AssistenteOperacao.ResponderNoPainel,
        _ => false
    };

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
