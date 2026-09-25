using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Controllers.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace JuridicoAI.Api.Tests;

public sealed class AuthApplicationServiceTests
{
    [Fact]
    public async Task Argon2id_GeraHashEValidaSemPersistirSenha()
    {
        var hasher = new Argon2idPasswordHasher();
        var hash = await hasher.HashAsync("senha-segura-123");
        Assert.StartsWith("$argon2id$", hash);
        Assert.DoesNotContain("senha-segura-123", hash);
        Assert.True(await hasher.VerifyAsync("senha-segura-123", hash));
        Assert.False(await hasher.VerifyAsync("incorreta", hash));
    }

    [Fact]
    public async Task Provisionar_NormalizaEmailECriaOwnerActiveAtomicamente()
    {
        var repository = new FakeAuthRepository();
        var service = new OrganizationApplicationService(repository, new FakePasswordHasher());
        var result = await service.ProvisionarAsync(new(" Escritório ", " João ", " JOAO@EXAMPLE.COM ", "senha-segura-123"), default);
        Assert.Equal(OrganizationStatusEnum.Active, result.Organization.Status);
        Assert.Equal("joao@example.com", result.User.Email);
        Assert.Equal("hash:senha-segura-123", result.User.PasswordHash);
        Assert.Equal(MembershipRoleEnum.Owner, result.Membership.Role);
        Assert.Equal(result.Organization.Id, result.Membership.OrganizationId);
        Assert.True(repository.ProvisionCalled);
    }

    [Fact]
    public async Task Register_CriaOrganizationOwnerESessaoComRefreshProtegido()
    {
        var repository = new FakeAuthRepository();
        var service = CreateService(repository);

        var response = await service.RegisterAsync(new(" Escritório Exemplo ", " João da Silva ",
            " JOAO@EXAMPLE.COM ", "senha-segura-123"), default);

        var organization = Assert.Single(repository.Organizations);
        var user = Assert.Single(repository.Users);
        var membership = Assert.Single(repository.Memberships);
        var session = Assert.Single(repository.Sessions);
        Assert.Equal("Escritório Exemplo", organization.Nome);
        Assert.Equal(OrganizationStatusEnum.Active, organization.Status);
        Assert.Equal("João da Silva", user.Nome);
        Assert.Equal("joao@example.com", user.Email);
        Assert.Equal("hash:senha-segura-123", user.PasswordHash);
        Assert.Equal(UserStatusEnum.Active, user.Status);
        Assert.Equal(MembershipRoleEnum.Owner, membership.Role);
        Assert.Equal(organization.Id, membership.OrganizationId);
        Assert.Equal(user.Id, membership.UserId);
        Assert.Equal("hash-" + response.RefreshToken, session.RefreshTokenHash);
        Assert.DoesNotContain("senha-segura-123", session.RefreshTokenHash);
        Assert.Equal(session.ExpiraEm, response.RefreshTokenExpiresAt);
        Assert.True(repository.RegisterCalled);

        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("RefreshToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(response.RefreshToken, json);

        var login = await service.LoginAsync(new("joao@example.com", "senha-segura-123"), default);
        Assert.Equal(user.Id, login.User.Id);
        Assert.Equal(organization.Id, login.Organization.Id);
        Assert.Equal(2, repository.Sessions.Count);
    }

    [Fact]
    public async Task Register_EmailExistenteRetornaConflitoSemCriarNovosDados()
    {
        var repository = new FakeAuthRepository();
        repository.AddUser();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<ConflitoException>(() => service.RegisterAsync(
            new("Org", "User", " USER@EXAMPLE.COM ", "senha-segura-123"), default));

        Assert.Equal("EMAIL_ALREADY_EXISTS", exception.Code);
        Assert.Single(repository.Users);
        Assert.Empty(repository.Organizations);
        Assert.Empty(repository.Sessions);
    }

    [Fact]
    public async Task Register_ConcorrentePermiteApenasUmUsuarioPorEmail()
    {
        var repository = new FakeAuthRepository();
        var service = CreateService(repository);
        var request = new RegisterRequest("Org", "User", "user@example.com", "senha-segura-123");

        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try { await service.RegisterAsync(request, default); return true; }
            catch (ConflitoException ex) when (ex.Code == "EMAIL_ALREADY_EXISTS") { return false; }
        }));

        Assert.Single(attempts, x => x);
        Assert.Single(repository.Users);
        Assert.Single(repository.Organizations);
        Assert.Single(repository.Memberships);
        Assert.Single(repository.Sessions);
    }

    [Fact]
    public async Task Register_FalhaNaPersistenciaNaoMantemDadosParciais()
    {
        var repository = new FakeAuthRepository { FailRegistration = true };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(
            new("Org", "User", "user@example.com", "senha-segura-123"), default));

        Assert.Empty(repository.Users);
        Assert.Empty(repository.Organizations);
        Assert.Empty(repository.Memberships);
        Assert.Empty(repository.Sessions);
    }

    [Fact]
    public async Task Register_ControllerRetorna201ECookieSeguroSemRefreshNoCorpo()
    {
        var repository = new FakeAuthRepository();
        var settings = new AuthSettings
        {
            AccessTokenTtlMinutes = 15, RefreshTokenTtlDays = 30, SecureRefreshCookie = true
        };
        var controller = new AuthController(CreateService(repository, settings), Options.Create(settings))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var action = await controller.Register(
            new("Org", "User", "user@example.com", "senha-segura-123"), default);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var response = Assert.IsType<AuthResponse>(result.Value);
        Assert.Empty(response.RefreshToken);
        var cookie = Assert.Single(controller.Response.Headers.SetCookie)
            ?? throw new InvalidOperationException("Cookie de refresh nao foi emitido.");
        Assert.Contains("juridicoai_refresh=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/auth", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_CriaSessaoERefreshRotacionaUmaUnicaVez()
    {
        var setup = Setup();
        var login = await setup.Service.LoginAsync(new("USER@EXAMPLE.COM", "correta"), default);
        Assert.Equal(setup.Organization.Id, login.Organization.Id);
        Assert.Single(setup.Repository.Sessions);
        var firstRefresh = login.RefreshToken;
        var refreshed = await setup.Service.RefreshAsync(firstRefresh, default);
        Assert.NotEqual(firstRefresh, refreshed.RefreshToken);
        await Assert.ThrowsAsync<NaoAutorizadoException>(() => setup.Service.RefreshAsync(firstRefresh, default));
    }

    [Fact]
    public async Task Login_NaoEnumeraUsuarioSenhaOuOrganizationInativa()
    {
        var setup = Setup();
        var missing = await Assert.ThrowsAsync<NaoAutorizadoException>(() => setup.Service.LoginAsync(new("missing@example.com", "x"), default));
        var wrong = await Assert.ThrowsAsync<NaoAutorizadoException>(() => setup.Service.LoginAsync(new("user@example.com", "errada"), default));
        setup.Organization.Status = OrganizationStatusEnum.Inactive;
        var inactive = await Assert.ThrowsAsync<NaoAutorizadoException>(() => setup.Service.LoginAsync(new("user@example.com", "correta"), default));
        Assert.Equal(missing.Message, wrong.Message);
        Assert.Equal(wrong.Message, inactive.Message);
    }

    [Fact]
    public async Task Login_ComMultiplasOrganizations_ExigeEscolhaEValidaMembership()
    {
        var setup = Setup();
        var other = setup.Repository.AddOrganization(setup.User, MembershipRoleEnum.Member);
        var conflict = await Assert.ThrowsAsync<ConflitoException>(() => setup.Service.LoginAsync(new("user@example.com", "correta"), default));
        Assert.Equal("ORGANIZATION_SELECTION_REQUIRED", conflict.Code);
        var selected = await setup.Service.LoginAsync(new("user@example.com", "correta", other.Id), default);
        Assert.Equal(other.Id, selected.Organization.Id);
        Assert.Equal(MembershipRoleEnum.Member, selected.Role);
        await Assert.ThrowsAsync<NaoAutorizadoException>(() => setup.Service.LoginAsync(new("user@example.com", "correta", Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Logout_RevogaSessaoEPermaneceIdempotente()
    {
        var setup = Setup();
        var login = await setup.Service.LoginAsync(new("user@example.com", "correta"), default);
        var session = Assert.Single(setup.Repository.Sessions);
        setup.Auth.Session = session.Id;
        await setup.Service.LogoutAsync(login.RefreshToken, default);
        await setup.Service.LogoutAsync(login.RefreshToken, default);
        await Assert.ThrowsAsync<NaoAutorizadoException>(() => setup.Service.RefreshAsync(login.RefreshToken, default));
    }

    [Fact]
    public void Jwt_UsaHs256ClaimsObrigatoriasERejeitaExpiradoOuAssinaturaInvalida()
    {
        const string secret = "segredo-de-testes-com-mais-de-trinta-e-dois-caracteres";
        var settings = new AuthSettings { JwtSecret = secret, Issuer = "issuer", Audience = "audience", AccessTokenTtlMinutes = 15 };
        var service = new JwtTokenService(Options.Create(settings));
        var now = DateTime.UtcNow;
        var user = new User { Id = Guid.NewGuid() };
        var organization = new Organization { Id = Guid.NewGuid() };
        var membership = new Membership { Role = MembershipRoleEnum.Owner };
        var session = new Session { Id = Guid.NewGuid() };
        var token = service.CriarAccessToken(user, organization, membership, session, now);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SecurityAlgorithms.HmacSha256, parsed.Header.Alg);
        Assert.Equal(user.Id.ToString(), parsed.Claims.Single(x => x.Type == "sub").Value);
        Assert.Equal(organization.Id.ToString(), parsed.Claims.Single(x => x.Type == "organizationId").Value);
        Assert.Equal("Owner", parsed.Claims.Single(x => x.Type == "role").Value);

        var valid = Validation(settings, secret);
        new JwtSecurityTokenHandler().ValidateToken(token, valid, out _);
        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, Validation(settings, new string('x', 64)), out _));
        var expired = service.CriarAccessToken(user, organization, membership, session, now.AddHours(-1));
        Assert.Throws<SecurityTokenExpiredException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(expired, valid, out _));
    }

    private static TokenValidationParameters Validation(AuthSettings settings, string secret) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidIssuer = settings.Issuer,
        ValidateAudience = true,
        ValidAudience = settings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    private static SetupResult Setup()
    {
        var repository = new FakeAuthRepository();
        var user = repository.AddUser();
        var organization = repository.AddOrganization(user, MembershipRoleEnum.Owner);
        var auth = new MutableAuthContext { User = user.Id, Organization = organization.Id };
        var service = new AuthApplicationService(repository, new FakePasswordHasher(), new FakeTokenService(), auth,
            Options.Create(new AuthSettings { AccessTokenTtlMinutes = 15, RefreshTokenTtlDays = 30 }));
        return new(service, repository, auth, user, organization);
    }

    private static AuthApplicationService CreateService(FakeAuthRepository repository, AuthSettings? settings = null) =>
        new(repository, new FakePasswordHasher(), new FakeTokenService(), new MutableAuthContext(),
            Options.Create(settings ?? new AuthSettings { AccessTokenTtlMinutes = 15, RefreshTokenTtlDays = 30 }));

    private sealed record SetupResult(AuthApplicationService Service, FakeAuthRepository Repository,
        MutableAuthContext Auth, User User, Organization Organization);

    private sealed class MutableAuthContext : IAuthContext
    {
        public Guid User { get; set; }
        public Guid Organization { get; set; }
        public Guid Session { get; set; }
        public Guid UserId => User; public Guid OrganizationId => Organization; public Guid SessionId => Session;
        public MembershipRoleEnum Role => MembershipRoleEnum.Owner;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public Task<string> HashAsync(string password, CancellationToken ct = default) => Task.FromResult("hash:" + password);
        public Task<bool> VerifyAsync(string password, string hash, CancellationToken ct = default) => Task.FromResult(hash == "hash:" + password);
    }

    private sealed class FakeTokenService : ITokenService
    {
        private int _next;
        public string CriarAccessToken(User u, Organization o, Membership m, Session s, DateTime now) => $"access:{u.Id}:{o.Id}:{m.Role}:{s.Id}";
        public string CriarRefreshToken() => "refresh-" + Interlocked.Increment(ref _next);
        public string HashRefreshToken(string token) => "hash-" + token;
    }

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public List<Organization> Organizations { get; } = [];
        public List<User> Users { get; } = [];
        public List<Membership> Memberships { get; } = [];
        public List<Session> Sessions { get; } = [];
        public bool ProvisionCalled { get; private set; }
        public bool RegisterCalled { get; private set; }
        public bool FailRegistration { get; init; }
        private readonly object _registrationLock = new();
        public User AddUser() { var now = DateTime.UtcNow; var x = new User { Id = Guid.NewGuid(), Nome = "User", Email = "user@example.com", PasswordHash = "hash:correta", Status = UserStatusEnum.Active, DataCriacao = now, DataAtualizacao = now }; Users.Add(x); return x; }
        public Organization AddOrganization(User user, MembershipRoleEnum role) { var now = DateTime.UtcNow; var o = new Organization { Id = Guid.NewGuid(), Nome = "Org", Status = OrganizationStatusEnum.Active, DataCriacao = now, DataAtualizacao = now }; Organizations.Add(o); Memberships.Add(new Membership { Id = Guid.NewGuid(), UserId = user.Id, OrganizationId = o.Id, Role = role, DataCriacao = now, DataAtualizacao = now }); return o; }
        public Task<Organization> CriarOrganizationAsync(Organization x, CancellationToken ct) { Organizations.Add(x); return Task.FromResult(x); }
        public Task<(Organization, User, Membership)> ProvisionarAsync(Organization o, User u, Membership m, CancellationToken ct) { ProvisionCalled = true; Organizations.Add(o); Users.Add(u); Memberships.Add(m); return Task.FromResult((o, u, m)); }
        public async Task<(Organization, User, Membership, Session)> RegistrarAsync(Organization o, User u, Membership m, Session s, CancellationToken ct)
        {
            await Task.Yield();
            lock (_registrationLock)
            {
                if (FailRegistration) throw new InvalidOperationException("Falha simulada.");
                if (Users.Any(x => x.Email == u.Email))
                    throw new ConflitoException("Ja existe um usuario com este email.", "EMAIL_ALREADY_EXISTS");
                RegisterCalled = true; Organizations.Add(o); Users.Add(u); Memberships.Add(m); Sessions.Add(s);
                return (o, u, m, s);
            }
        }
        public Task<User?> ObterUserPorEmailAsync(string email, CancellationToken ct) => Task.FromResult(Users.SingleOrDefault(x => x.Email == email));
        public Task<User?> ObterUserAsync(Guid id, CancellationToken ct) => Task.FromResult(Users.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<MembershipDetails>> ListarMembershipsAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<MembershipDetails>>(Memberships.Where(x => x.UserId == userId).Select(x => new MembershipDetails(x, Organizations.Single(o => o.Id == x.OrganizationId))).ToList());
        public async Task<MembershipDetails?> ObterMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct) => (await ListarMembershipsAsync(userId, ct)).SingleOrDefault(x => x.Organization.Id == organizationId);
        public Task<Session> CriarSessionAsync(Session x, CancellationToken ct) { Sessions.Add(x); return Task.FromResult(x); }
        public Task<Session?> ObterSessionPorRefreshHashAsync(string hash, CancellationToken ct) => Task.FromResult(Sessions.SingleOrDefault(x => x.RefreshTokenHash == hash));
        public Task<Session?> ObterSessionAsync(Guid id, CancellationToken ct) => Task.FromResult(Sessions.SingleOrDefault(x => x.Id == id));
        public Task<bool> RotacionarRefreshAsync(Guid id, string current, string next, DateTime now, CancellationToken ct) { var x = Sessions.SingleOrDefault(x => x.Id == id && x.RefreshTokenHash == current && x.RevogadaEm is null && x.ExpiraEm > now); if (x is null) return Task.FromResult(false); x.RefreshTokenHash = next; x.UltimoUsoEm = now; return Task.FromResult(true); }
        public Task RevogarSessionAsync(Guid id, DateTime now, CancellationToken ct) { var x = Sessions.SingleOrDefault(x => x.Id == id); if (x is not null) x.RevogadaEm ??= now; return Task.CompletedTask; }
    }
}
