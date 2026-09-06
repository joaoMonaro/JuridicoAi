using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Abstractions;

public interface IAuthRepository
{
    Task<Organization> CriarOrganizationAsync(Organization item, CancellationToken ct);
    Task<(Organization Organization, User User, Membership Membership)> ProvisionarAsync(
        Organization organization, User user, Membership membership, CancellationToken ct);
    Task<User?> ObterUserPorEmailAsync(string email, CancellationToken ct);
    Task<User?> ObterUserAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<MembershipDetails>> ListarMembershipsAsync(Guid userId, CancellationToken ct);
    Task<MembershipDetails?> ObterMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct);
    Task<Session> CriarSessionAsync(Session session, CancellationToken ct);
    Task<Session?> ObterSessionPorRefreshHashAsync(string hash, CancellationToken ct);
    Task<Session?> ObterSessionAsync(Guid id, CancellationToken ct);
    Task<bool> RotacionarRefreshAsync(Guid sessionId, string hashAtual, string novoHash, DateTime agora, CancellationToken ct);
    Task RevogarSessionAsync(Guid sessionId, DateTime agora, CancellationToken ct);
}

public interface IPasswordHasher
{
    Task<string> HashAsync(string password, CancellationToken ct = default);
    Task<bool> VerifyAsync(string password, string encodedHash, CancellationToken ct = default);
}

public interface ITokenService
{
    string CriarAccessToken(User user, Organization organization, Membership membership, Session session, DateTime agora);
    string CriarRefreshToken();
    string HashRefreshToken(string token);
}
