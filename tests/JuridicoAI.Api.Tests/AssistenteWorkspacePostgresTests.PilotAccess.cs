using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task Piloto_OrganizacaoExistenteAcessaAgenteSemLiberacaoAdicional()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var features = PilotFeatures(context);

        Assert.Empty(await context.OrganizationFeatures.ToArrayAsync());
        var snapshot = await features.ObterAsync(db.OrgId, default);

        Assert.True(snapshot.Habilitado);
        Assert.Equal(Enum.GetValues<OrganizationFeatureEnum>().Order(), snapshot.Features.Order());
        Assert.Equal(Enum.GetValues<AssistenteModo>().Order(), snapshot.Modos.Order());
        await features.ExigirModoAsync(db.OrgId, AssistenteModo.Redigir, default);
        Assert.False((await features.ObterAsync(Guid.NewGuid(), default)).Habilitado);

        await context.Organizations.Where(x => x.Id == db.OrgId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OrganizationStatusEnum.Inactive));
        Assert.False((await features.ObterAsync(db.OrgId, default)).Habilitado);
    }

    [ReviewPostgresFact]
    public async Task Piloto_ProvisionamentoComCadastroFechadoPermiteLoginEAcessoAoAgente()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var repository = new AuthRepository(context);
        var hasher = new Argon2idPasswordHasher();
        var created = await new OrganizationApplicationService(repository, hasher).ProvisionarAsync(
            new("Escritório piloto", "Advogado", "advogado@example.test", "senha-ficticia-123"), default);
        var settings = Options.Create(new AuthSettings { JwtSecret = new string('t', 64) });
        var auth = new AuthApplicationService(repository, hasher, new JwtTokenService(settings),
            new TestAuthContext(created.Organization.Id, created.User.Id), settings);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => auth.RegisterAsync(
            new("Outro escritório", "Outro usuário", "outro@example.test", "senha-ficticia-123"), default));
        var login = await auth.LoginAsync(new("advogado@example.test", "senha-ficticia-123"), default);
        var snapshot = await PilotFeatures(context).ObterAsync(login.Organization.Id, default);

        Assert.Equal(created.User.Id, login.User.Id);
        Assert.True(snapshot.Habilitado);
        Assert.Contains(OrganizationFeatureEnum.AI_DOCUMENT_GENERATION, snapshot.Features);
        Assert.Empty(await context.OrganizationFeatures.ToArrayAsync());
    }

    [ReviewPostgresFact]
    public async Task Piloto_BloqueiosAdministrativosSaoRevalidadosEAuditadosSemAfetarOutraOrganizacao()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var other = await new OrganizationApplicationService(new AuthRepository(context), new Argon2idPasswordHasher())
            .ProvisionarAsync(new("Outro escritório", "Outro advogado", "outro@example.test", "senha-ficticia-123"), default);
        var now = DateTime.UtcNow;
        context.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(), OrganizationId = db.OrgId, UserId = db.UserId,
            Role = MembershipRoleEnum.Owner, DataCriacao = now, DataAtualizacao = now
        });
        await context.SaveChangesAsync();
        var repository = new OrganizationFeatureRepository(context);
        var features = PilotFeatures(context);

        await repository.DefinirAsync(db.OrgId, OrganizationFeatureEnum.AI_EDIT, false,
            db.UserId, "Bloqueio administrativo de edição", now, default);
        var denied = await Assert.ThrowsAsync<AssistenteIaException>(() =>
            features.ExigirModoAsync(db.OrgId, AssistenteModo.Redigir, default));
        Assert.Equal("AI_FEATURE_NOT_ENABLED", denied.Code);
        Assert.DoesNotContain(AssistenteModo.Redigir, (await features.ObterAsync(db.OrgId, default)).Modos);
        await features.ExigirModoAsync(db.OrgId, AssistenteModo.Perguntar, default);
        await features.ExigirModoAsync(other.Organization.Id, AssistenteModo.Redigir, default);

        await repository.DefinirAsync(db.OrgId, OrganizationFeatureEnum.AI_EDIT, true,
            db.UserId, "Restauração administrativa de edição", now, default);
        await features.ExigirModoAsync(db.OrgId, AssistenteModo.Redigir, default);

        await repository.DefinirAsync(db.OrgId, OrganizationFeatureEnum.AI_ASSISTANT, false,
            db.UserId, "Bloqueio administrativo do agente", now, default);
        Assert.False((await features.ObterAsync(db.OrgId, default)).Habilitado);
        Assert.True((await features.ObterAsync(other.Organization.Id, default)).Habilitado);
        Assert.Equal(3, await context.AiFeatureChangeAudit.CountAsync());
    }

    private static AssistenteFeatureAccessService PilotFeatures(JuridicoAiDbContext context) =>
        new(new OrganizationFeatureRepository(context), new AiOperationalControlRepository(context),
            new Monitor(new AssistenteIaSettings { Enabled = true, ConversationV2Enabled = true, ReviewV2Enabled = true }));
}
