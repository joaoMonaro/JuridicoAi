using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Application.Abstractions;

public interface IOrganizationFeatureRepository
{
    Task<IReadOnlySet<OrganizationFeatureEnum>> ListarHabilitadasAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task DefinirAsync(
        Guid organizationId,
        OrganizationFeatureEnum feature,
        bool enabled,
        Guid? updatedById,
        DateTime updatedAt,
        CancellationToken cancellationToken);
}

public interface IAiUsageLedgerRepository
{
    Task AdicionarAsync(AiUsageLedgerEntry entry, CancellationToken cancellationToken);
}

public interface IAssistenteFeatureAccess
{
    Task<AssistenteFeatureSnapshot> ObterAsync(Guid organizationId, CancellationToken cancellationToken);
    Task ExigirModoAsync(Guid organizationId, AssistenteModo modo, CancellationToken cancellationToken);
}

public sealed record AssistenteFeatureSnapshot(
    bool Habilitado,
    IReadOnlyList<OrganizationFeatureEnum> Features,
    IReadOnlyList<AssistenteModo> Modos);

public interface IAssistenteUsageLimiter
{
    ValueTask<IAsyncDisposable> AdquirirAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);
}

public interface ICorrelationContext
{
    Guid CorrelationId { get; }
}

public sealed record AssistenteUsageRecord(
    Guid OrganizationId,
    Guid UserId,
    Guid PecaId,
    Guid SolicitacaoId,
    Guid ClientRequestId,
    Guid CorrelationId,
    AssistenteModo Modo,
    AssistenteOperacao? Operacao,
    string Provider,
    string Model,
    string PromptVersion,
    int AttemptCount,
    int? InputTokens,
    int? CachedInputTokens,
    int? OutputTokens,
    string Status,
    string? ErrorCode,
    long DurationMs);

public interface IAssistenteUsageRecorder
{
    Task RegistrarAsync(AssistenteUsageRecord record, CancellationToken cancellationToken);
}
