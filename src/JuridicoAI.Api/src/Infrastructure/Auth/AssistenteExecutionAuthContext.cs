using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Infrastructure.Auth;

// Scoped, server-only identity. The worker binds it after checking the current
// membership and active user/organization in the database; no token is fabricated.
public sealed class AssistenteBackgroundIdentity
{
    internal (Guid OrganizationId, Guid UserId, MembershipRoleEnum Role)? Value { get; private set; }
    internal Guid? CorrelationId { get; private set; }
    internal void Bind(Guid organizationId, Guid userId, MembershipRoleEnum role, Guid correlationId)
    { Value = (organizationId, userId, role); CorrelationId = correlationId; }
}
public sealed class AssistenteExecutionAuthContext(HttpAuthContext http, AssistenteBackgroundIdentity identity) : IAuthContext
{
    public Guid OrganizationId => identity.Value?.OrganizationId ?? http.OrganizationId;
    public Guid UserId => identity.Value?.UserId ?? http.UserId;
    public Guid SessionId => identity.Value is null ? http.SessionId : Guid.Empty;
    public MembershipRoleEnum Role => identity.Value?.Role ?? http.Role;
}
