using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class OrganizationFeatureRepository(JuridicoAiDbContext context) : IOrganizationFeatureRepository
{
    // O acesso ao piloto já libera o agente; registros persistidos são overrides
    // administrativos. Novas permissões devem definir seu próprio padrão.
    private static readonly OrganizationFeatureEnum[] DefaultFeatures =
    [
        OrganizationFeatureEnum.AI_ASSISTANT,
        OrganizationFeatureEnum.AI_EDIT,
        OrganizationFeatureEnum.AI_REVIEW,
        OrganizationFeatureEnum.AI_DOCUMENT_GENERATION
    ];

    public async Task<IReadOnlySet<OrganizationFeatureEnum>> ListarHabilitadasAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!await context.Organizations.AnyAsync(
                x => x.Id == organizationId && x.Status == OrganizationStatusEnum.Active,
                cancellationToken))
            return new HashSet<OrganizationFeatureEnum>();

        var disabled = await context.OrganizationFeatures
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && !x.Enabled)
            .Select(x => x.Feature)
            .ToListAsync(cancellationToken);
        return DefaultFeatures.Except(disabled).ToHashSet();
    }

    public async Task DefinirAsync(
        Guid organizationId,
        OrganizationFeatureEnum feature,
        bool enabled,
        Guid updatedById,
        string reason,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        if (!await context.Organizations.AnyAsync(x => x.Id == organizationId, cancellationToken))
            throw new InvalidOperationException("Organização não encontrada.");
        if (!await context.Users.AnyAsync(
                x => x.Id == updatedById && x.Status == UserStatusEnum.Active,
                cancellationToken) ||
            !await context.Memberships.AnyAsync(
                x => x.OrganizationId == organizationId &&
                     x.UserId == updatedById &&
                     x.Role == MembershipRoleEnum.Owner,
                cancellationToken))
            throw new InvalidOperationException("O operador deve ser um owner ativo da organização.");
        reason = NormalizeReason(reason);

        var current = await context.OrganizationFeatures.FindAsync(
            [organizationId, feature],
            cancellationToken);
        if (current is null)
        {
            context.OrganizationFeatures.Add(new OrganizationFeature
            {
                OrganizationId = organizationId,
                Feature = feature,
                Enabled = enabled,
                UpdatedById = updatedById,
                UpdatedAt = updatedAt
            });
        }
        else
        {
            current.Enabled = enabled;
            current.UpdatedById = updatedById;
            current.UpdatedAt = updatedAt;
        }

        context.AiFeatureChangeAudit.Add(new AiFeatureChangeAudit
        {
            Id = Guid.NewGuid(),
            Scope = AiFeatureScopeEnum.Organization,
            OrganizationId = organizationId,
            Feature = feature.ToString(),
            Enabled = enabled,
            ChangedById = updatedById,
            ChangedAt = updatedAt,
            Reason = reason
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeReason(string reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 500)
            throw new InvalidOperationException("A justificativa deve ter entre 1 e 500 caracteres.");
        return normalized;
    }
}
