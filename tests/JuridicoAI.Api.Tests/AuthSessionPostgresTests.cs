using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task AuthPrincipal_RefreshAtualizaAntesDoLogoutERespostaTardiaNaoRestauraAcesso()
        => await MainSessionRaceAsync(beforeUpdate: false);

    [ReviewPostgresFact]
    public async Task AuthPrincipal_LogoutEntreLeituraEAtualizacaoImpedeRefresh()
        => await MainSessionRaceAsync(beforeUpdate: true);

    private static async Task MainSessionRaceAsync(bool beforeUpdate)
    {
        await using var db = await Database.Create();
        var gate = new RefreshRaceGate(beforeUpdate);
        await using var api = await MainSessionApi.CreateAsync(db, gate);
        using var client = api.Client();
        var account = await api.AccountAsync("race");
        var initial = await MainSessionApi.LoginAsync(client, account);
        var independent = await MainSessionApi.LoginAsync(client, account);
        Assert.Equal(HttpStatusCode.OK, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", initial));
        using var refreshRequest = MainSessionApi.Request(HttpMethod.Post, "/auth/refresh", initial);
        var pendingRefresh = client.SendAsync(refreshRequest);

        try
        {
            await gate.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using (var inspect = db.Context())
            {
                var session = await inspect.Sessions.AsNoTracking().SingleAsync(s => s.Id == MainSessionId(initial));
                var tokenService = new JwtTokenService(Options.Create(MainSessionApi.Settings));
                Assert.Equal(beforeUpdate, session.RefreshTokenHash == tokenService.HashRefreshToken(initial.Refresh));
            }
            using var logout = MainSessionApi.Request(HttpMethod.Post, "/auth/logout", initial);
            using var loggedOut = await client.SendAsync(logout);
            Assert.Equal(HttpStatusCode.NoContent, loggedOut.StatusCode);
            Assert.Contains("expires=", string.Join(';', loggedOut.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
        }
        finally { gate.Release.TrySetResult(); }

        using var refreshed = await pendingRefresh;
        Assert.Equal(beforeUpdate ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, refreshed.StatusCode);
        if (!beforeUpdate)
        {
            // A resposta tardia contém credenciais novas, mas a sessão já foi revogada.
            var late = await MainSessionApi.ReadTokensAsync(refreshed);
            Assert.Equal(initial.Refresh.Split('.')[0], late.Refresh.Split('.')[0]);
            Assert.Equal(HttpStatusCode.Unauthorized, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", late));
            Assert.Equal(HttpStatusCode.Unauthorized, await MainStatusAsync(client, HttpMethod.Post, "/auth/refresh", late));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", initial));
        Assert.Equal(HttpStatusCode.Unauthorized, await MainStatusAsync(client, HttpMethod.Post, "/auth/refresh", initial));
        Assert.Equal(HttpStatusCode.NoContent, await MainStatusAsync(client, HttpMethod.Post, "/auth/logout", initial));
        Assert.Equal(HttpStatusCode.OK, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", independent));
        await using var verify = db.Context();
        Assert.NotNull((await verify.Sessions.AsNoTracking().SingleAsync(s => s.Id == MainSessionId(initial))).RevogadaEm);
        Assert.Null((await verify.Sessions.AsNoTracking().SingleAsync(s => s.Id == MainSessionId(independent))).RevogadaEm);
    }

    [ReviewPostgresFact]
    public async Task AuthPrincipal_DoisRefreshsConcorrentesPermitemUmaUnicaRotacaoELogoutAntigoRevogaVencedor()
    {
        await using var db = await Database.Create();
        var gate = new RefreshRaceGate(beforeUpdate: true, participants: 2);
        await using var api = await MainSessionApi.CreateAsync(db, gate);
        using var client = api.Client();
        var initial = await MainSessionApi.LoginAsync(client, await api.AccountAsync("parallel"));
        using var first = MainSessionApi.Request(HttpMethod.Post, "/auth/refresh", initial);
        using var second = MainSessionApi.Request(HttpMethod.Post, "/auth/refresh", initial);
        var firstTask = client.SendAsync(first);
        var secondTask = client.SendAsync(second);
        try { await gate.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { gate.Release.TrySetResult(); }
        using var one = await firstTask;
        using var two = await secondTask;
        Assert.Single(new[] { one, two }, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(new[] { one, two }, response => response.StatusCode == HttpStatusCode.Unauthorized);
        var winner = await MainSessionApi.ReadTokensAsync(one.IsSuccessStatusCode ? one : two);
        Assert.Equal(HttpStatusCode.OK, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", winner));
        Assert.Equal(HttpStatusCode.NoContent, await MainStatusAsync(client, HttpMethod.Post, "/auth/logout", initial));
        Assert.Equal(HttpStatusCode.Unauthorized, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", winner));
    }

    [ReviewPostgresFact]
    public async Task AuthPrincipal_MigrationPreservaTokensLegadosEImpedeDowngradeQuePerderiaRevogacao()
    {
        const string previous = "20261119113211_AddAssistantProgressMonitoring";
        await using var db = await Database.Create();
        var options = Options.Create(MainSessionApi.Settings);
        var tokens = new JwtTokenService(options);
        var legacy = tokens.CriarRefreshToken();
        var otherLegacy = tokens.CriarRefreshToken();
        var sessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var context = db.Context())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(previous);
            context.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(), UserId = db.UserId, OrganizationId = db.OrgId,
                Role = MembershipRoleEnum.Owner, DataCriacao = now, DataAtualizacao = now
            });
            await context.SaveChangesAsync();
            foreach (var entry in new[] { (sessionId, legacy), (otherSessionId, otherLegacy) })
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO sessions (id, user_id, organization_id, refresh_token_hash, expira_em, data_criacao, data_atualizacao)
                    VALUES ({entry.Item1}, {db.UserId}, {db.OrgId}, {tokens.HashRefreshToken(entry.Item2)}, {now.AddDays(7)}, {now}, {now})
                    """);
            await migrator.MigrateAsync();
            Assert.All(await context.Sessions.AsNoTracking().ToArrayAsync(), session =>
                Assert.Equal(session.RefreshTokenHash, session.RevocationTokenHash));
            var auth = new AuthApplicationService(new AuthRepository(context), new Argon2idPasswordHasher(), tokens,
                new TestAuthContext(db.OrgId, db.UserId), options);
            var refreshed = await auth.RefreshAsync(legacy, default);
            Assert.Equal(legacy, refreshed.RefreshToken.Split('.')[0]);
            await Assert.ThrowsAsync<NaoAutorizadoException>(() => auth.RefreshAsync(legacy, default));
            await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous));
            await auth.LogoutAsync(legacy, default);
            await Assert.ThrowsAsync<NaoAutorizadoException>(() => auth.RefreshAsync(refreshed.RefreshToken, default));
            var independent = await auth.RefreshAsync(otherLegacy, default);
            Assert.Equal(otherLegacy, independent.RefreshToken.Split('.')[0]);
        }
        await using var verify = db.Context();
        Assert.NotNull((await verify.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId)).RevogadaEm);
        Assert.Null((await verify.Sessions.AsNoTracking().SingleAsync(s => s.Id == otherSessionId)).RevogadaEm);
    }

    [ReviewPostgresFact]
    public async Task AuthPrincipal_JwtERefreshRecusamContaOrganizacaoVinculoOuSessaoInvalidos()
    {
        await using var db = await Database.Create();
        await using var api = await MainSessionApi.CreateAsync(db);
        using var client = api.Client();
        foreach (var scenario in new[] { "user", "organization", "membership", "expired" })
        {
            var account = await api.AccountAsync(scenario);
            var issued = await MainSessionApi.LoginAsync(client, account);
            Assert.Equal(HttpStatusCode.OK, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", issued));
            await using var context = db.Context();
            switch (scenario)
            {
                case "user":
                    await context.Users.Where(u => u.Id == account.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatusEnum.Inactive));
                    break;
                case "organization":
                    await context.Organizations.Where(o => o.Id == account.OrganizationId).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrganizationStatusEnum.Inactive));
                    break;
                case "membership":
                    await context.Memberships.Where(m => m.UserId == account.UserId).ExecuteDeleteAsync();
                    break;
                default:
                    await context.Sessions.Where(s => s.Id == MainSessionId(issued)).ExecuteUpdateAsync(s => s.SetProperty(s => s.ExpiraEm, DateTime.UtcNow.AddMinutes(-1)));
                    break;
            }
            Assert.Equal(HttpStatusCode.Unauthorized, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", issued));
            Assert.Equal(HttpStatusCode.Unauthorized, await MainStatusAsync(client, HttpMethod.Post, "/auth/refresh", issued));
        }
    }

    [ReviewPostgresFact]
    public async Task AuthPrincipal_JwtRealIsolaOrganizacoesEConversasAnexosPrivadosEntreUsuarios()
    {
        await using var db = await Database.Create();
        await using var api = await MainSessionApi.CreateAsync(db);
        using var client = api.Client();
        var owner = await api.AccountAsync("owner");
        var member = await api.AccountAsync("member", owner.OrganizationId);
        var foreign = await api.AccountAsync("foreign");
        var a = await MainSessionApi.LoginAsync(client, owner);
        var m = await MainSessionApi.LoginAsync(client, member);
        var b = await MainSessionApi.LoginAsync(client, foreign);
        var caseA = (await MainJsonAsync(client, HttpMethod.Post, "/casos", a, new { titulo = "Caso privado A" })).GetProperty("id").GetGuid();
        var caseB = (await MainJsonAsync(client, HttpMethod.Post, "/casos", b, new { titulo = "Caso B" })).GetProperty("id").GetGuid();
        var pieceA = (await MainJsonAsync(client, HttpMethod.Post, $"/casos/{caseA}/pecas", a, new { titulo = "Peça A" })).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, await MainStatusAsync(client, HttpMethod.Get, $"/pecas/{pieceA}", m));
        foreach (var path in new[] { $"/casos/{caseA}", $"/casos/{caseA}/pecas", $"/pecas/{pieceA}" })
            Assert.Equal(HttpStatusCode.NotFound, await MainStatusAsync(client, HttpMethod.Get, path, b));
        Assert.Equal(HttpStatusCode.NotFound, await MainStatusAsync(client, HttpMethod.Delete, $"/pecas/{pieceA}?versao=1", b));
        Assert.Equal(HttpStatusCode.NotFound, await MainStatusAsync(client, HttpMethod.Post, $"/pecas/{pieceA}/exportar", b, new { formato = "pdf", versao = 1 }));
        Assert.Equal(HttpStatusCode.OK, await MainStatusAsync(client, HttpMethod.Post, $"/pecas/{pieceA}/exportar", a, new { formato = "docx", versao = 1 }));
        var list = await MainJsonAsync(client, HttpMethod.Get, "/casos", b);
        Assert.Equal(caseB, Assert.Single(list.EnumerateArray()).GetProperty("id").GetGuid());
        var conversation = (await MainJsonAsync(client, HttpMethod.Post, $"/assistente/v2/pecas/{pieceA}/conversa", a)).GetProperty("id").GetGuid();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(Guid.NewGuid().ToString()), "clientUploadId");
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Documento privado do titular.")), "arquivo", "privado.txt");
        using var upload = MainSessionApi.Request(HttpMethod.Post, $"/assistente/v2/conversas/{conversation}/anexos", a);
        upload.Content = form;
        using var uploaded = await client.SendAsync(upload);
        uploaded.EnsureSuccessStatusCode();
        using var file = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());
        var document = file.RootElement.GetProperty("id").GetGuid();
        var download = $"/assistente/v2/conversas/{conversation}/anexos/{document}/arquivo";
        Assert.Equal(HttpStatusCode.OK, await MainStatusAsync(client, HttpMethod.Get, download, a));
        foreach (var other in new[] { m, b })
            foreach (var path in new[] { $"/assistente/v2/conversas/{conversation}/mensagens", $"/assistente/v2/conversas/{conversation}/anexos", download })
                Assert.Equal(HttpStatusCode.NotFound, await MainStatusAsync(client, HttpMethod.Get, path, other));
        Assert.Equal(HttpStatusCode.NoContent, await MainStatusAsync(client, HttpMethod.Post, "/auth/logout", a));
        Assert.Equal(HttpStatusCode.Unauthorized, await MainStatusAsync(client, HttpMethod.Get, download, a));
        Assert.Equal(HttpStatusCode.OK, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", m));
        Assert.Equal(HttpStatusCode.OK, await MainStatusAsync(client, HttpMethod.Get, "/auth/me", b));
    }

    private static Guid MainSessionId(MainTokens tokens) => Guid.Parse(new JwtSecurityTokenHandler()
        .ReadJwtToken(tokens.Access).Claims.Single(claim => claim.Type == "sessionId").Value);

    private static async Task<HttpStatusCode> MainStatusAsync(HttpClient client, HttpMethod method, string path, MainTokens tokens, object? body = null)
    {
        using var request = MainSessionApi.Request(method, path, tokens, body);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<JsonElement> MainJsonAsync(HttpClient client, HttpMethod method, string path, MainTokens tokens, object? body = null)
    {
        using var request = MainSessionApi.Request(method, path, tokens, body);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }
}
