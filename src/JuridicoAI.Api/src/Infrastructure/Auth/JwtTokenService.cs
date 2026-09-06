using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JuridicoAI.Api.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<AuthSettings> options) : ITokenService
{
    private readonly AuthSettings _settings = options.Value;

    public string CriarAccessToken(User user, Organization organization, Membership membership, Session session, DateTime agora)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecret)), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("organizationId", organization.Id.ToString()),
            new Claim("sessionId", session.Id.ToString()),
            new Claim("role", membership.Role.ToString())
        };
        var token = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims, agora,
            agora.AddMinutes(_settings.AccessTokenTtlMinutes), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CriarRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
