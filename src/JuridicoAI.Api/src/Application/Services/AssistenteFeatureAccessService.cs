using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteFeatureAccessService(
    IOrganizationFeatureRepository repository,
    IOptionsMonitor<AssistenteIaSettings> options) : IAssistenteFeatureAccess
{
    public async Task<AssistenteFeatureSnapshot> ObterAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Enabled)
            return new(false, [], []);

        var features = await repository.ListarHabilitadasAsync(organizationId, cancellationToken);
        if (!features.Contains(OrganizationFeatureEnum.AI_ASSISTANT))
            return new(false, [], []);

        // AI_DOCUMENT_GENERATION permanece persistível para rollout futuro, mas
        // não é anunciada até que o endpoint de geração integral exista.
        var visibleFeatures = features
            .Where(x => x != OrganizationFeatureEnum.AI_DOCUMENT_GENERATION)
            .Order()
            .ToArray();

        var modes = Enum.GetValues<AssistenteModo>()
            .Where(mode => RequiredFeature(mode) is null || features.Contains(RequiredFeature(mode)!.Value))
            .ToArray();
        return new(true, visibleFeatures, modes);
    }

    public async Task ExigirModoAsync(
        Guid organizationId,
        AssistenteModo modo,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Enabled)
            throw new AssistenteIaException(
                StatusCodes.Status503ServiceUnavailable,
                "AI_UNAVAILABLE",
                "O assistente de IA não está habilitado.",
                retryable: true);

        var snapshot = await ObterAsync(organizationId, cancellationToken);
        if (!snapshot.Habilitado || !snapshot.Modos.Contains(modo))
            throw new AssistenteIaException(
                StatusCodes.Status403Forbidden,
                "AI_FEATURE_NOT_ENABLED",
                "Esta funcionalidade de IA não está habilitada para a organização.");
    }

    private static OrganizationFeatureEnum? RequiredFeature(AssistenteModo mode) => mode switch
    {
        AssistenteModo.Perguntar => null,
        AssistenteModo.Redigir or AssistenteModo.Reescrever => OrganizationFeatureEnum.AI_EDIT,
        AssistenteModo.Revisar => OrganizationFeatureEnum.AI_REVIEW,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
