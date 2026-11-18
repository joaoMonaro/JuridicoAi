using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class AiOperationalControlRepository(JuridicoAiDbContext context)
    : IAiOperationalControlRepository
{
    public async Task<bool> ObterAssistenteHabilitadoAsync(CancellationToken cancellationToken)
    {
        var enabled = await context.AiOperationalControls
            .AsNoTracking()
            .Where(x => x.Feature == AiOperationalFeatureEnum.AI_ENABLED)
            .Select(x => (bool?)x.Enabled)
            .SingleOrDefaultAsync(cancellationToken);
        return enabled ?? true;
    }

    public async Task DefinirAssistenteHabilitadoAsync(
        bool enabled,
        Guid updatedById,
        string reason,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(
                x => x.Id == updatedById && x.Status == UserStatusEnum.Active,
                cancellationToken))
            throw new InvalidOperationException("Operador ativo não encontrado.");

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 500)
            throw new InvalidOperationException("A justificativa deve ter entre 1 e 500 caracteres.");

        var current = await context.AiOperationalControls.FindAsync(
            [AiOperationalFeatureEnum.AI_ENABLED],
            cancellationToken);
        if (current is null)
        {
            context.AiOperationalControls.Add(new AiOperationalControl
            {
                Feature = AiOperationalFeatureEnum.AI_ENABLED,
                Enabled = enabled,
                UpdatedById = updatedById,
                UpdatedAt = updatedAt,
                Reason = normalizedReason
            });
        }
        else
        {
            current.Enabled = enabled;
            current.UpdatedById = updatedById;
            current.UpdatedAt = updatedAt;
            current.Reason = normalizedReason;
        }

        context.AiFeatureChangeAudit.Add(new AiFeatureChangeAudit
        {
            Id = Guid.NewGuid(),
            Scope = AiFeatureScopeEnum.Global,
            OrganizationId = null,
            Feature = AiOperationalFeatureEnum.AI_ENABLED.ToString(),
            Enabled = enabled,
            ChangedById = updatedById,
            ChangedAt = updatedAt,
            Reason = normalizedReason
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
