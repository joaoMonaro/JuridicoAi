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
    public Func<int, int, CancellationToken, Task>? BeforeFinancialGeneration { get; set; }
    public Func<string, CancellationToken, Task>? ReleasePendingFinancial { get; set; }
    public Func<AssistenteProviderCall, Task<AssistenteCallSettlementOutcome?>>? SettleFinancialCall { get; set; }
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
    public Func<AssistenteProviderCall, AssistenteCallSettlementOutcome?, Task>? RecordCallWithOutcome { get; set; }
    public int AttemptedCalls { get; private set; }
    public int RecordedCalls { get; private set; }
    public bool StoppedBeforeProviderCall { get; private set; }
    public Action<string>? SetScope { get; set; }
    public Func<CancellationToken, Task>? OnLimit { get; set; }
    public Func<CancellationToken, Task>? BeforeCall { get; set; }
    public AssistenteCoberturaDocumentalDto Coverage => coverage();
    public Task<string> ExecuteAsync(string name, string arguments, CancellationToken ct) => execute(name, arguments, ct);
    public void MarkStoppedBeforeProviderCall() => StoppedBeforeProviderCall = true;

    public async Task ReserveRoundAsync(int inputTokens, int maxOutputTokens, CancellationToken ct)
    {
        if (BeforeFinancialGeneration is not null) await BeforeFinancialGeneration(inputTokens, maxOutputTokens, ct);
        if (BeforeGeneration is not null)
        {
            try { await BeforeGeneration(inputTokens, maxOutputTokens, ct); }
            catch
            {
                if (ReleasePendingFinancial is not null)
                    await ReleasePendingFinancial("AI_GENERATION_LIMIT", CancellationToken.None);
                throw;
            }
        }
    }

    public async Task RecordAsync(AssistenteProviderCall call)
    {
        AttemptedCalls++;
        AssistenteCallSettlementOutcome? outcome = null;
        try
        {
            if (SettleFinancialCall is not null) outcome = await SettleFinancialCall(call);
            if (RecordCallWithOutcome is not null) await RecordCallWithOutcome(call, outcome);
            else if (RecordCall is not null) await RecordCall(call);
        }
        catch (Exceptions.AssistenteIaException)
        {
            throw;
        }
        catch (Exception ex) when (SettleFinancialCall is not null && ex is not OperationCanceledException)
        {
            throw new Exceptions.AssistenteIaException(
                StatusCodes.Status503ServiceUnavailable,
                "AI_USAGE_PERSISTENCE_FAILED",
                "Não foi possível registrar o consumo da solicitação. Novas chamadas foram bloqueadas por segurança.",
                retryable: true,
                innerException: ex);
        }
        RecordedCalls++;
        if (AfterCall is not null) await AfterCall(call);
    }
}

public sealed record AssistenteProviderCall(int Ordinal, string Model, int? InputTokens, int? CachedInputTokens,
    int? OutputTokens, string Status, string? ErrorCode, long DurationMs,
    bool Dispatched = true, bool SafeToRelease = false);
