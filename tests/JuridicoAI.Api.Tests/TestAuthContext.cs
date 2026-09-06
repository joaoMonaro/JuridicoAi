using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Tests;

internal sealed class TestAuthContext(Guid? organizationId = null, Guid? userId = null) : IAuthContext
{
    public Guid UserId { get; } = userId ?? Guid.NewGuid();
    public Guid OrganizationId { get; } = organizationId ?? Guid.NewGuid();
    public Guid SessionId { get; } = Guid.NewGuid();
    public MembershipRoleEnum Role => MembershipRoleEnum.Owner;
}
