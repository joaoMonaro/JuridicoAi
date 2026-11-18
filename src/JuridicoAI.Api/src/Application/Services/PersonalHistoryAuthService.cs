using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace JuridicoAI.Api.Application.Services;

public sealed record PersonalHistoryAuthResponse(string AccessToken, int ExpiresIn, AuthUserDto User,
    [property: JsonIgnore] string RefreshToken, [property: JsonIgnore] DateTime RefreshTokenExpiresAt);
public sealed class PersonalHistoryAuthService(JuridicoAiDbContext db, IPasswordHasher passwords, ITokenService tokens, IOptions<AuthSettings> options)
{
    public async Task<PersonalHistoryAuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = OrganizationApplicationService.NormalizarEmail(request.Email);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user?.Status != UserStatusEnum.Active || string.IsNullOrEmpty(request.Senha) || request.Senha.Length > 1000 || !await passwords.VerifyAsync(request.Senha, user.PasswordHash, ct)) throw Invalid();
        var revocation = tokens.CriarRefreshToken();
        var refresh = revocation + "." + tokens.CriarRefreshToken(); var now = DateTime.UtcNow;
        var session = new PersonalHistorySession { Id = Guid.NewGuid(), UserId = user.Id, RevocationTokenHash = tokens.HashRefreshToken(revocation), RefreshTokenHash = tokens.HashRefreshToken(refresh),
            CriadaEm = now, AtualizadaEm = now, ExpiraEm = now.AddDays(7) };
        db.Add(session); await db.SaveChangesAsync(ct); return Response(user, session, refresh, now);
    }
    public async Task<PersonalHistoryAuthResponse> RefreshAsync(string? refresh, CancellationToken ct)
    {
        if (Parts(refresh) is null) throw Invalid();
        var hash = tokens.HashRefreshToken(refresh!); var now = DateTime.UtcNow;
        var session = await db.Set<PersonalHistorySession>().AsNoTracking().SingleOrDefaultAsync(s => s.RefreshTokenHash == hash && s.RevogadaEm == null && s.ExpiraEm > now, ct) ?? throw Invalid();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == session.UserId && u.Status == UserStatusEnum.Active, ct) ?? throw Invalid();
        var next = Parts(refresh)![0] + "." + tokens.CriarRefreshToken();
        if (await db.Set<PersonalHistorySession>().Where(s => s.Id == session.Id && s.RefreshTokenHash == hash && s.RevogadaEm == null && s.ExpiraEm > now)
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.RefreshTokenHash, tokens.HashRefreshToken(next)).SetProperty(s => s.AtualizadaEm, now), ct) != 1) throw Invalid();
        return Response(user, session, next, now);
    }
    public async Task LogoutAsync(string? refresh, CancellationToken ct)
    {
        var parts = Parts(refresh); if (parts is null) return;
        // A rotated cookie cannot renew access, but its stable, random revocation secret
        // can still close this session. This prevents refresh/logout races across tabs.
        var hash = tokens.HashRefreshToken(parts[0]);
        await db.Set<PersonalHistorySession>().Where(s => s.RevocationTokenHash == hash && s.RevogadaEm == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevogadaEm, DateTime.UtcNow), ct);
    }
    private PersonalHistoryAuthResponse Response(User user, PersonalHistorySession session, string refresh, DateTime now)
    {
        var token = new JwtSecurityToken(options.Value.Issuer, PersonalHistoryAuthentication.Audience,
            [new Claim("sub", user.Id.ToString()), new Claim("sessionId", session.Id.ToString())], now, now.AddMinutes(15),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.JwtSecret)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), 900, new(user.Id, user.Nome, user.Email), refresh, session.ExpiraEm);
    }
    private static string[]? Parts(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512) return null;
        var parts = token.Split('.');
        return parts.Length == 2 && parts.All(p => p.Length is >= 80 and <= 100) ? parts : null;
    }
    private static NaoAutorizadoException Invalid() => new("Credenciais pessoais inválidas.");
}
