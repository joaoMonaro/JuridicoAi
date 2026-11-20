using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Controllers.Auth;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Http;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    private sealed record MainAccount(Guid OrganizationId, Guid UserId, string Email);
    private sealed record MainTokens(string Access, string Cookie, string Refresh);

    private sealed class RefreshRaceGate(bool beforeUpdate, int participants = 1) : DbCommandInterceptor
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        private async Task PauseAsync(DbCommand command, CancellationToken ct)
        {
            if (!command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                !command.CommandText.Contains("refresh_token_hash", StringComparison.Ordinal)) return;
            var count = Interlocked.Increment(ref _arrivals);
            if (count > participants) return;
            if (count == participants) Arrived.TrySetResult();
            await Release.Task.WaitAsync(ct);
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (beforeUpdate) await PauseAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (!beforeUpdate) await PauseAsync(command, cancellationToken);
            return result;
        }
    }

    private sealed class MainSessionApi(WebApplication app, Uri baseAddress, RefreshRaceGate? gate) : IAsyncDisposable
    {
        public static AuthSettings Settings { get; } = new()
        {
            JwtSecret = "main-session-synthetic-test-signing-key-123456789",
            RegistrationEnabled = false, SecureRefreshCookie = false
        };
        private const string Password = "Main-session-test-123!";

        public HttpClient Client() => new(new HttpClientHandler { UseCookies = false })
        { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(20) };

        public async Task<MainAccount> AccountAsync(string tag, Guid? organizationId = null)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var email = tag + "@example.test";
            if (organizationId is null)
            {
                var created = await new OrganizationApplicationService(new AuthRepository(db), hasher)
                    .ProvisionarAsync(new("Escritório " + tag, tag, email, Password), default);
                return new(created.Organization.Id, created.User.Id, created.User.Email);
            }
            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(), Nome = tag, Email = email, PasswordHash = await hasher.HashAsync(Password),
                Status = UserStatusEnum.Active, DataCriacao = now, DataAtualizacao = now
            };
            db.Users.Add(user);
            db.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(), UserId = user.Id, OrganizationId = organizationId.Value,
                Role = MembershipRoleEnum.Member, DataCriacao = now, DataAtualizacao = now
            });
            await db.SaveChangesAsync();
            return new(organizationId.Value, user.Id, email);
        }

        public static async Task<MainTokens> LoginAsync(HttpClient client, MainAccount account)
        {
            using var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(account.Email, Password));
            response.EnsureSuccessStatusCode();
            return await ReadTokensAsync(response);
        }

        public static async Task<MainTokens> ReadTokensAsync(HttpResponseMessage response)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("juridicoai_refresh=", StringComparison.Ordinal))
                .Split(';', 2)[0];
            return new(json.RootElement.GetProperty("accessToken").GetString()!, cookie,
                Uri.UnescapeDataString(cookie["juridicoai_refresh=".Length..]));
        }

        public static HttpRequestMessage Request(HttpMethod method, string path, MainTokens tokens, object? body = null)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
            request.Headers.Add("Cookie", tokens.Cookie);
            if (body is not null) request.Content = JsonContent.Create(body);
            return request;
        }

        public static async Task<MainSessionApi> CreateAsync(Database database, RefreshRaceGate? gate = null)
        {
            await using var context = database.Context();
            var connection = context.Database.GetConnectionString();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddDbContext<JuridicoAiDbContext>(options =>
            {
                options.UseNpgsql(connection);
                if (gate is not null) options.AddInterceptors(gate);
            });
            builder.Services.AddSingleton<IOptions<AuthSettings>>(Options.Create(Settings));
            builder.Services.AddScoped<IAuthRepository, AuthRepository>();
            builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
            builder.Services.AddSingleton<ITokenService, JwtTokenService>();
            builder.Services.AddScoped<AuthApplicationService>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IAuthContext, HttpAuthContext>();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => OrganizationAuthentication.Configure(options, Settings))
                .AddJwtBearer(PersonalHistoryAuthentication.Scheme, options => PersonalHistoryAuthentication.Configure(options, Settings));
            builder.Services.AddAuthorization();
            builder.Services.AddRateLimiter(AuthRateLimitPolicy.Configure);
            builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly)
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddScoped<ICasoRepository, CasoRepository>();
            builder.Services.AddScoped<IPecaRepository, PecaRepository>();
            builder.Services.AddScoped<CasoApplicationService>();
            builder.Services.AddScoped<PecaApplicationService>();
            builder.Services.AddSingleton<IFileStorage>(new DocumentMemoryStorage());
            builder.Services.AddSingleton<IOptions<DocumentStorageSettings>>(Options.Create(new DocumentStorageSettings()));
            builder.Services.AddScoped<IAssistenteWorkspaceRepository, AssistenteWorkspaceRepository>();
            builder.Services.AddScoped<AssistenteWorkspaceService>();
            builder.Services.AddSingleton<IAssistenteFeatureAccess>(new Features());
            builder.Services.AddSingleton<IAssistenteTransparencyService>(new Transparency());
            builder.Services.AddSingleton<IOptionsMonitor<AssistenteIaSettings>>(new Monitor(new()
            { ReviewV2Enabled = true, ConversationV2Enabled = true }));
            builder.Services.AddSingleton<AssistentePatchCompiler>();
            builder.Services.AddSingleton<IOptions<AssistenteDocumentSettings>>(Options.Create(new AssistenteDocumentSettings { Enabled = true }));
            builder.Services.AddScoped<AssistenteDocumentService>();
            builder.Services.AddSingleton<PecaDocxGenerator>();
            builder.Services.AddSingleton<IPecaPdfConverter>(new UnexpectedPdfConverter());
            builder.Services.AddScoped<PecaExportApplicationService>();
            var app = builder.Build();
            app.UseMiddleware<ApiExceptionMiddleware>();
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new Uri(address), gate);
        }

        public async ValueTask DisposeAsync()
        {
            gate?.Release.TrySetResult();
            await app.DisposeAsync();
        }
    }

    private sealed class UnexpectedPdfConverter : IPecaPdfConverter
    {
        public Task<byte[]> ConvertAsync(byte[] docx, CancellationToken ct) =>
            throw new InvalidOperationException("A exportação alheia deve ser bloqueada antes do conversor.");
    }
}
