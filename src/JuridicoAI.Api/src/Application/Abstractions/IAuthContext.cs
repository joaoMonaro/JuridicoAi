using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Abstractions;

public interface IAuthContext
{
    Guid UserId { get; }
    Guid OrganizationId { get; }
    Guid SessionId { get; }
    MembershipRole Role { get; }
}
