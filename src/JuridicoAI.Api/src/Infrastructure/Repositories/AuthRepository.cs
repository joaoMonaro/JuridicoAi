using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class AuthRepository(JuridicoAiDbContext context) : IAuthRepository
{
    public async Task<Organization> CriarOrganizationAsync(Organization item, CancellationToken ct)
    { context.Organizations.Add(item); await context.SaveChangesAsync(ct); return item; }

    public async Task<(Organization, User, Membership)> ProvisionarAsync(
        Organization organization, User user, Membership membership, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        context.AddRange(organization, user, membership);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (organization, user, membership);
    }

    public async Task<(Organization, User, Membership, Session)> RegistrarAsync(
        Organization organization, User user, Membership membership, Session session, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            context.AddRange(organization, user, membership, session);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (organization, user, membership, session);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                   { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_users_email" })
        {
            await transaction.RollbackAsync(ct);
            throw new ConflitoException("Ja existe um usuario com este email.", "EMAIL_ALREADY_EXISTS");
        }
    }

    public Task<User?> ObterUserPorEmailAsync(string email, CancellationToken ct) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email, ct);
    public Task<User?> ObterUserAsync(Guid id, CancellationToken ct) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<MembershipDetails>> ListarMembershipsAsync(Guid userId, CancellationToken ct) =>
        await context.Memberships.AsNoTracking().Where(x => x.UserId == userId)
            .Join(context.Organizations.AsNoTracking(), x => x.OrganizationId, x => x.Id,
                (membership, organization) => new MembershipDetails(membership, organization)).ToListAsync(ct);

    public Task<MembershipDetails?> ObterMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct) =>
        context.Memberships.AsNoTracking().Where(x => x.UserId == userId && x.OrganizationId == organizationId)
            .Join(context.Organizations.AsNoTracking(), x => x.OrganizationId, x => x.Id,
                (membership, organization) => new MembershipDetails(membership, organization)).SingleOrDefaultAsync(ct);

    public async Task<Session> CriarSessionAsync(Session session, CancellationToken ct)
    { context.Sessions.Add(session); await context.SaveChangesAsync(ct); return session; }

    public Task<Session?> ObterSessionPorRefreshHashAsync(string hash, CancellationToken ct) =>
        context.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, ct);
    public Task<Session?> ObterSessionAsync(Guid id, CancellationToken ct) =>
        context.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> RotacionarRefreshAsync(Guid id, string atual, string novo, DateTime agora, CancellationToken ct) =>
        await context.Sessions.Where(x => x.Id == id && x.RefreshTokenHash == atual && x.RevogadaEm == null && x.ExpiraEm > agora)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RefreshTokenHash, novo)
                .SetProperty(x => x.UltimoUsoEm, agora).SetProperty(x => x.DataAtualizacao, agora), ct) == 1;

    public Task RevogarSessionAsync(Guid id, DateTime agora, CancellationToken ct) =>
        context.Sessions.Where(x => x.Id == id && x.RevogadaEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevogadaEm, agora).SetProperty(x => x.DataAtualizacao, agora), ct);
}
