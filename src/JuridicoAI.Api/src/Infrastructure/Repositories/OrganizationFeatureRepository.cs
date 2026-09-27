using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class OrganizationFeatureRepository(JuridicoAiDbContext context) : IOrganizationFeatureRepository
{
    public async Task<IReadOnlySet<OrganizationFeatureEnum>> ListarHabilitadasAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        (await context.OrganizationFeatures
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Enabled)
            .Select(x => x.Feature)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task DefinirAsync(
        Guid organizationId,
        OrganizationFeatureEnum feature,
        bool enabled,
        Guid? updatedById,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        if (!await context.Organizations.AnyAsync(x => x.Id == organizationId, cancellationToken))
            throw new InvalidOperationException("Organização não encontrada.");

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

        await context.SaveChangesAsync(cancellationToken);
    }
}
