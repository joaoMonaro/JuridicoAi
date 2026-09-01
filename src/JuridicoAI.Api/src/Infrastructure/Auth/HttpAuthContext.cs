using System.Security.Claims;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Infrastructure.Auth;

public sealed class HttpAuthContext(IHttpContextAccessor accessor) : IAuthContext
{
    private ClaimsPrincipal User => accessor.HttpContext?.User
        ?? throw new NaoAutorizadoException("Autenticacao obrigatoria.");

    public Guid UserId => ClaimGuid("sub");
    public Guid OrganizationId => ClaimGuid("organizationId");
    public Guid SessionId => ClaimGuid("sessionId");
    public MembershipRole Role => Enum.TryParse<MembershipRole>(User.FindFirstValue("role"), out var role)
        ? role : throw new NaoAutorizadoException("Token invalido.");

    private Guid ClaimGuid(string type) => Guid.TryParse(User.FindFirstValue(type), out var value)
        ? value : throw new NaoAutorizadoException("Token invalido.");
}
