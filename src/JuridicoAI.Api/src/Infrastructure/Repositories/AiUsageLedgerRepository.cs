using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class AiUsageLedgerRepository(JuridicoAiDbContext context) : IAiUsageLedgerRepository
{
    public async Task AdicionarAsync(AiUsageLedgerEntry entry, CancellationToken cancellationToken)
    {
        context.AiUsageLedger.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }
}
