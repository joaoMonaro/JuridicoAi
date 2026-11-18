using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace JuridicoAI.Api.Infrastructure.Auth;

public interface IPersonalHistoryContext { Guid UserId { get; } }
public sealed class PersonalHistoryContext(IHttpContextAccessor accessor) : IPersonalHistoryContext
{
    public Guid UserId => accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user &&
        user.HasClaim(JwtRegisteredClaimNames.Aud, PersonalHistoryAuthentication.Audience) && !user.HasClaim(c => c.Type == "organizationId") &&
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : throw new NaoAutorizadoException("Sessão pessoal obrigatória.");
}
public static class PersonalHistoryAuthentication
{
    public const string Scheme = "PersonalHistory";
    public const string Audience = "JuridicoAI.History";
    public static void Configure(JwtBearerOptions options, AuthSettings settings)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new()
        {
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidateIssuer = true, ValidIssuer = settings.Issuer,
            ValidateAudience = true, ValidAudience = Audience, ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub, AuthenticationType = Scheme
        };
        options.Events = new() { OnTokenValidated = async context =>
        {
            var user = context.Principal;
            if (user is null || user.HasClaim(c => c.Type is "organizationId" or "role") ||
                !Guid.TryParse(user.FindFirstValue("sub"), out var uid) || !Guid.TryParse(user.FindFirstValue("sessionId"), out var sid))
            { context.Fail("Sessão pessoal inválida."); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<JuridicoAiDbContext>();
            if (!await db.Set<Domain.Entities.PersonalHistorySession>().AnyAsync(s => s.Id == sid && s.UserId == uid && s.RevogadaEm == null && s.ExpiraEm > DateTime.UtcNow &&
                db.Users.Any(u => u.Id == uid && u.Status == UserStatusEnum.Active), context.HttpContext.RequestAborted)) context.Fail("Sessão pessoal inválida.");
        } };
    }
}
