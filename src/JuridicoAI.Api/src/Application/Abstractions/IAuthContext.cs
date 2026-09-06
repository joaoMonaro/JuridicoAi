using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Application.Abstractions;

public interface IAuthContext
{
    Guid UserId { get; }
    Guid OrganizationId { get; }
    Guid SessionId { get; }
    MembershipRoleEnum Role { get; }
}
