namespace JuridicoAI.Api.Application.Abstractions;

public enum AssistenteWorkerKind { Generation, Analysis, Document, Deletion }
public enum AssistenteProviderOperation { Generation, InputTokens }
public enum AssistenteCallOutcome { Succeeded, Failed, Cancelled }

/// <summary>Only technical, bounded dimensions. No prompts, results, IDs or exception messages.</summary>
public interface IAssistenteTelemetry
{
    void Stage(string stage, double milliseconds);
    void ProviderCall(AssistenteProviderOperation operation, AssistenteCallOutcome outcome, double milliseconds);
    void ProviderTokens(int? input, int? cachedInput, int? output);
    void WorkerFailure(AssistenteWorkerKind worker);
    void WorkerLeaseLost(AssistenteWorkerKind worker);
}
