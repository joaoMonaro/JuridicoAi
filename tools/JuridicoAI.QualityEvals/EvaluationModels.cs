using JuridicoAI.Api.Application.DTOs;

namespace JuridicoAI.QualityEvals;

public enum EvaluationCategory
{
    Reescrita,
    Redacao,
    Analise,
    Fatos,
    Fontes,
    Preservacao
}

public sealed record EvaluationSuiteDefinition(
    string SuiteVersion,
    int ExpectedCaseCount,
    int? ExpectedCasesPerCategory,
    IReadOnlyList<string> CaseFiles,
    IReadOnlyDictionary<EvaluationCategory, int>? ExpectedCategoryCounts = null);

public sealed record EvaluationCase(
    string Id,
    int Version,
    EvaluationCategory Category,
    IReadOnlyList<string> Tags,
    AssistenteModo Mode,
    AssistenteOperacao Operation,
    string Instruction,
    EvaluationInput Input,
    EvaluationExpected Expected,
    IReadOnlyList<string> AutomaticFailures);

public sealed record EvaluationInput(
    string Scope,
    string DocumentText,
    string? AdditionalContext,
    IReadOnlyList<EvaluationSource> SuppliedSources);

public sealed record EvaluationSource(string Id, string Title, string Content);

public sealed record EvaluationExpected(
    IReadOnlyList<string> Required,
    IReadOnlyList<string> Forbidden,
    IReadOnlyDictionary<string, string> MaterialFacts,
    bool RequiresUnverifiedReferenceAlert = false,
    bool ShouldRefuseExternalResearch = false,
    IReadOnlyList<string>? ForbiddenPhrases = null);

public sealed record LoadedEvaluationSuite(
    string RootPath,
    EvaluationSuiteDefinition Definition,
    IReadOnlyList<EvaluationCase> Cases);

public sealed record AutomaticCheckResult(
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Warnings)
{
    public bool Passed => Failures.Count == 0;
}

public sealed record EvaluationExecutionResult(
    string CaseId,
    EvaluationCategory Category,
    string Status,
    string? Title,
    string? Text,
    string? Summary,
    AssistenteOperacao? SuggestedOperation,
    IReadOnlyList<AssistenteAlertaDto> Alerts,
    IReadOnlyList<AssistenteAchadoDto> Findings,
    IReadOnlyList<string> AutomaticFailures,
    IReadOnlyList<string> AutomaticWarnings,
    int? InputTokens,
    int? CachedInputTokens,
    int? OutputTokens,
    long DurationMs,
    string? ErrorCode);

public sealed record EvaluationExecutionManifest(
    string SuiteVersion,
    string ApplicationCommit,
    string PromptVersion,
    string Provider,
    string Model,
    DateTime ExecutedAt,
    int CaseCount,
    int? TotalInputTokens,
    int? TotalCachedInputTokens,
    int? TotalOutputTokens,
    string ReviewStatus);

public sealed record EvaluationRecheckManifest(
    string SourceSuiteVersion,
    string RecheckSuiteVersion,
    string SourceApplicationCommit,
    string SourcePromptVersion,
    string Provider,
    string Model,
    DateTime SourceExecutedAt,
    string EvaluatorVersion,
    DateTime RecheckedAt,
    int CaseCount,
    int AutomaticFailureCount,
    int ExecutionFailureCount,
    string SourceResultsFile);
