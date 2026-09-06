using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class Organization
{
    public Guid Id { get; init; }
    public string Nome { get; set; } = string.Empty;
    public OrganizationStatusEnum Status { get; set; } = OrganizationStatusEnum.Active;
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}

public sealed class User
{
    public Guid Id { get; init; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserStatusEnum Status { get; set; } = UserStatusEnum.Active;
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}

public sealed class Membership
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public MembershipRoleEnum Role { get; set; }
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
}

public sealed class Session
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid OrganizationId { get; init; }
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; init; }
    public DateTime? RevogadaEm { get; set; }
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; set; }
    public DateTime? UltimoUsoEm { get; set; }
}
