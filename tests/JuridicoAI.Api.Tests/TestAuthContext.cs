using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Tests;

internal sealed class TestAuthContext(Guid? organizationId = null, Guid? userId = null) : IAuthContext
{
    public Guid UserId { get; } = userId ?? Guid.NewGuid();
    public Guid OrganizationId { get; } = organizationId ?? Guid.NewGuid();
    public Guid SessionId { get; } = Guid.NewGuid();
    public MembershipRole Role => MembershipRole.Owner;
}
