using System.IdentityModel.Tokens.Jwt;
using System.Text;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace JuridicoAI.Api.Infrastructure.Auth;

public static class OrganizationAuthentication
{
    public static void Configure(JwtBearerOptions options, AuthSettings settings)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true, ValidIssuer = settings.Issuer,
            ValidateAudience = true, ValidAudience = settings.Audience,
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub, RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents { OnTokenValidated = ValidateSessionAsync };
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst("sessionId")?.Value, out var sessionId) ||
            !Guid.TryParse(principal?.FindFirst("sub")?.Value, out var userId) ||
            !Guid.TryParse(principal?.FindFirst("organizationId")?.Value, out var organizationId))
        { context.Fail("Sessao invalida."); return; }

        var repository = context.HttpContext.RequestServices.GetRequiredService<IAuthRepository>();
        var ct = context.HttpContext.RequestAborted;
        var session = await repository.ObterSessionAsync(sessionId, ct);
        var user = await repository.ObterUserAsync(userId, ct);
        var membership = await repository.ObterMembershipAsync(userId, organizationId, ct);
        if (session is null || session.UserId != userId || session.OrganizationId != organizationId ||
            session.RevogadaEm is not null || session.ExpiraEm <= DateTime.UtcNow ||
            user?.Status != UserStatusEnum.Active || membership?.Organization.Status != OrganizationStatusEnum.Active ||
            membership.Membership.Role.ToString() != principal?.FindFirst("role")?.Value)
            context.Fail("Sessao invalida.");
    }
}
