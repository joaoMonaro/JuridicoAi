using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class AuthApplicationService(
    IAuthRepository repository, IPasswordHasher passwordHasher, ITokenService tokens,
    IAuthContext authContext, IOptions<AuthSettings> options)
{
    private readonly AuthSettings _settings = options.Value;

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = OrganizationApplicationService.NormalizarEmail(request.Email);
        var user = await repository.ObterUserPorEmailAsync(email, ct);
        if (user is null || user.Status != UserStatus.Active || !await passwordHasher.VerifyAsync(request.Senha, user.PasswordHash, ct))
            throw CredenciaisInvalidas();
        var memberships = await repository.ListarMembershipsAsync(user.Id, ct);
        MembershipDetails selected;
        if (request.OrganizationId is { } organizationId)
            selected = memberships.SingleOrDefault(x => x.Organization.Id == organizationId) ?? throw CredenciaisInvalidas();
        else if (memberships.Count == 1) selected = memberships[0];
        else if (memberships.Count > 1)
            throw new ConflitoException("Selecione uma Organization para continuar.", "ORGANIZATION_SELECTION_REQUIRED");
        else throw CredenciaisInvalidas();
        if (selected.Organization.Status != OrganizationStatus.Active) throw CredenciaisInvalidas();
        return await CriarSessaoAsync(user, selected, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) throw CredenciaisInvalidas();
        var hash = tokens.HashRefreshToken(refreshToken);
        var session = await repository.ObterSessionPorRefreshHashAsync(hash, ct);
        var agora = DateTime.UtcNow;
        if (session is null || session.RevogadaEm is not null || session.ExpiraEm <= agora) throw CredenciaisInvalidas();
        var user = await repository.ObterUserAsync(session.UserId, ct);
        var membership = await repository.ObterMembershipAsync(session.UserId, session.OrganizationId, ct);
        if (user is null || user.Status != UserStatus.Active || membership is null || membership.Organization.Status != OrganizationStatus.Active)
            throw CredenciaisInvalidas();
        var novoRefresh = tokens.CriarRefreshToken();
        if (!await repository.RotacionarRefreshAsync(session.Id, hash, tokens.HashRefreshToken(novoRefresh), agora, ct))
            throw CredenciaisInvalidas();
        var access = tokens.CriarAccessToken(user, membership.Organization, membership.Membership, session, agora);
        return Response(access, novoRefresh, user, membership);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var session = await repository.ObterSessionPorRefreshHashAsync(tokens.HashRefreshToken(refreshToken), ct);
        if (session is not null) await repository.RevogarSessionAsync(session.Id, DateTime.UtcNow, ct);
    }

    public async Task<MeResponse> MeAsync(CancellationToken ct)
    {
        var user = await repository.ObterUserAsync(authContext.UserId, ct) ?? throw new NaoAutorizadoException("Sessao invalida.");
        var membership = await repository.ObterMembershipAsync(user.Id, authContext.OrganizationId, ct)
            ?? throw new NaoAutorizadoException("Sessao invalida.");
        return new MeResponse(user.Id, user.Nome, user.Email,
            new AuthOrganizationDto(membership.Organization.Id, membership.Organization.Nome), membership.Membership.Role);
    }

    private async Task<AuthResponse> CriarSessaoAsync(User user, MembershipDetails details, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var refresh = tokens.CriarRefreshToken();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrganizationId = details.Organization.Id,
            RefreshTokenHash = tokens.HashRefreshToken(refresh),
            ExpiraEm = agora.AddDays(_settings.RefreshTokenTtlDays),
            DataCriacao = agora,
            DataAtualizacao = agora
        };
        await repository.CriarSessionAsync(session, ct);
        return Response(tokens.CriarAccessToken(user, details.Organization, details.Membership, session, agora), refresh, user, details);
    }

    private AuthResponse Response(string access, string refresh, User user, MembershipDetails details) =>
        new(access, _settings.AccessTokenTtlMinutes * 60, new(user.Id, user.Nome, user.Email),
            new(details.Organization.Id, details.Organization.Nome), details.Membership.Role, refresh);
    private static NaoAutorizadoException CredenciaisInvalidas() => new("Credenciais invalidas.");
}
