using System.Text.Json;
using JuridicoAI.Api.Application.DTOs;

namespace JuridicoAI.Api.Application.Abstractions;

// Only server-created sessions can dispatch tools. Never deserialize this from
// a client request or persist delegates/provider reasoning in conversation JSON.
public sealed class AssistenteDocumentTools(JsonElement definitions,
    Func<string, string, CancellationToken, Task<string>> execute,
    Func<AssistenteCoberturaDocumentalDto> coverage,
    int maxRounds, int maxTotalTokens, int maxContextCharacters)
{
    public bool OnlyPlanFullDraft { get; init; }
    public Func<int, int, CancellationToken, Task>? BeforeGeneration { get; set; }
    public Func<AssistenteProviderCall, Task>? AfterCall { get; set; }
    public Func<AssistenteIaProviderResult?>? CompleteAfterTool { get; set; }
    public bool RequireDurableUsage { get; init; }
    public bool HasDocumentTools { get; init; } = true;
    public string AdditionalInstructions { get; init; } = string.Empty;
    public string AdditionalPolicyVersion { get; init; } = string.Empty;
    public Func<string?>? RequiredTool { get; init; }
    public JsonElement Definitions { get; } = definitions;
    public int MaxRounds { get; set; } = maxRounds;
    public int MaxTotalTokens { get; set; } = maxTotalTokens;
    public int MaxContextCharacters { get; } = maxContextCharacters;
    public int MaxInputTokens { get; set; } = 60_000;
    public int? InitialInputTokens { get; set; }
    public Func<AssistenteProviderCall, Task>? RecordCall { get; set; }
    public int RecordedCalls { get; private set; }
    public Action<string>? SetScope { get; set; }
    public Func<CancellationToken, Task>? OnLimit { get; set; }
    public Func<CancellationToken, Task>? BeforeCall { get; set; }
    public AssistenteCoberturaDocumentalDto Coverage => coverage();
    public Task<string> ExecuteAsync(string name, string arguments, CancellationToken ct) => execute(name, arguments, ct);
    public async Task RecordAsync(AssistenteProviderCall call)
    { if (RecordCall is not null) await RecordCall(call); RecordedCalls++; if (AfterCall is not null) await AfterCall(call); }
}

public sealed record AssistenteProviderCall(int Ordinal, string Model, int? InputTokens, int? CachedInputTokens,
    int? OutputTokens, string Status, string? ErrorCode, long DurationMs);
