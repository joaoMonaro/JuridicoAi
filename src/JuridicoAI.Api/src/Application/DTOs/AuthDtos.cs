using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using System.Text.Json.Serialization;

namespace JuridicoAI.Api.Application.DTOs;

public sealed record LoginRequest(string Email, string Senha, Guid? OrganizationId = null);
public sealed record AuthUserDto(Guid Id, string Nome, string Email);
public sealed record AuthOrganizationDto(Guid Id, string Nome);
public sealed record AuthResponse(string AccessToken, int ExpiresIn, AuthUserDto User,
    AuthOrganizationDto Organization, MembershipRoleEnum Role, [property: JsonIgnore] string RefreshToken);
public sealed record MeResponse(Guid Id, string Nome, string Email, AuthOrganizationDto Organization, MembershipRoleEnum Role);
public sealed record OrganizationOptionDto(Guid OrganizationId, string OrganizationName, MembershipRoleEnum Role);
public sealed record CriarOrganizationRequest(string Nome);
public sealed record ProvisionOrganizationRequest(string OrganizationName, string OwnerName, string OwnerEmail, string OwnerPassword);

public sealed record MembershipDetails(Membership Membership, Organization Organization);
