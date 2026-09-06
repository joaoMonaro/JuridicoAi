using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Application.Services;

public sealed class OrganizationApplicationService(IAuthRepository repository, IPasswordHasher passwordHasher)
{
    public Task<Organization> CriarAsync(CriarOrganizationRequest request, CancellationToken ct)
    {
        ValidarNome(request.Nome, "nome");
        var agora = DateTime.UtcNow;
        return repository.CriarOrganizationAsync(new Organization
        { Id = Guid.NewGuid(), Nome = request.Nome.Trim(), Status = OrganizationStatusEnum.Active, DataCriacao = agora, DataAtualizacao = agora }, ct);
    }

    public async Task<(Organization Organization, User User, Membership Membership)> ProvisionarAsync(
        ProvisionOrganizationRequest request, CancellationToken ct)
    {
        ValidarNome(request.OrganizationName, "organizationName");
        ValidarNome(request.OwnerName, "ownerName");
        var email = NormalizarEmail(request.OwnerEmail);
        ValidarSenha(request.OwnerPassword);
        if (await repository.ObterUserPorEmailAsync(email, ct) is not null)
            throw new ConflitoException("Ja existe um usuario com este email.", "EMAIL_ALREADY_EXISTS");
        var agora = DateTime.UtcNow;
        var organization = new Organization { Id = Guid.NewGuid(), Nome = request.OrganizationName.Trim(), Status = OrganizationStatusEnum.Active, DataCriacao = agora, DataAtualizacao = agora };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Nome = request.OwnerName.Trim(),
            Email = email,
            PasswordHash = await passwordHasher.HashAsync(request.OwnerPassword, ct),
            Status = UserStatusEnum.Active,
            DataCriacao = agora,
            DataAtualizacao = agora
        };
        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserId = user.Id,
            Role = MembershipRoleEnum.Owner,
            DataCriacao = agora,
            DataAtualizacao = agora
        };
        return await repository.ProvisionarAsync(organization, user, membership, ct);
    }

    internal static string NormalizarEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@') || normalized.Length > 320)
            throw new RequisicaoInvalidaException("Email invalido.");
        return normalized;
    }

    private static void ValidarNome(string? value, string campo)
    { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 200) throw new RequisicaoInvalidaException($"O campo {campo} e obrigatorio e deve ter ate 200 caracteres."); }
    private static void ValidarSenha(string? value)
    { if (string.IsNullOrWhiteSpace(value) || value.Length < 12 || value.Length > 200) throw new RequisicaoInvalidaException("A senha deve ter entre 12 e 200 caracteres."); }
}
