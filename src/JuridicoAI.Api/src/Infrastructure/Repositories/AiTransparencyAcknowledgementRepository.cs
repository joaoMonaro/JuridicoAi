using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class AiTransparencyAcknowledgementRepository(JuridicoAiDbContext context)
    : IAiTransparencyAcknowledgementRepository
{
    public Task<AiTransparencyAcknowledgement?> ObterAsync(
        Guid organizationId,
        Guid userId,
        string noticeVersion,
        CancellationToken cancellationToken) =>
        context.AiTransparencyAcknowledgements
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId &&
                     x.UserId == userId &&
                     x.NoticeVersion == noticeVersion,
                cancellationToken);

    public async Task RegistrarAsync(
        AiTransparencyAcknowledgement acknowledgement,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO ai_transparency_acknowledgements
                (id, organization_id, user_id, notice_version, acknowledged_at)
            VALUES
                ({{acknowledgement.Id}}, {{acknowledgement.OrganizationId}}, {{acknowledgement.UserId}},
                 {{acknowledgement.NoticeVersion}}, {{acknowledgement.AcknowledgedAt}})
            ON CONFLICT (organization_id, user_id, notice_version) DO NOTHING
            """, cancellationToken);
    }
}
