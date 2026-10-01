using System.Text.Json.Serialization;
using System.Text;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Http;
using JuridicoAI.Api.Infrastructure.Auth;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using JuridicoAI.Api.Infrastructure.Storage;
using JuridicoAI.Api.Infrastructure.AI;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "LocalFrontend";

builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<PostgresSettings>(builder.Configuration.GetSection("Postgres"));
builder.Services.Configure<ScraperSettings>(builder.Configuration.GetSection("Scraper"));
builder.Services.Configure<DocumentStorageSettings>(builder.Configuration.GetSection("DocumentStorage"));
builder.Services.AddOptions<AssistenteIaSettings>().Bind(builder.Configuration.GetSection("AssistenteIa"))
    .Validate(x => x.TimeoutSeconds > 0 && x.MaxInstructionCharacters > 0 &&
                   x.MaxDocumentContextCharacters > 0 && x.MaxAdditionalContextCharacters > 0 &&
                   x.MaxTotalContextCharacters > 0 && x.MaxInputTokens > 0 && x.MaxOutputCharacters > 0 &&
                   x.MaxOutputTokens > 0 && x.MaxRequestBodyBytes > 0 &&
                   x.RequestsPerUserPerMinute > 0 && x.ConcurrentRequestsPerUser > 0 &&
                   x.RequestsPerOrganizationPerMinute > 0 && x.ConcurrentRequestsPerOrganization > 0 &&
                   x.InputCostPerMillionTokensUsd >= 0 && x.CachedInputCostPerMillionTokensUsd >= 0 &&
                   x.OutputCostPerMillionTokensUsd >= 0 && !string.IsNullOrWhiteSpace(x.PricingVersion),
        "Os limites de AssistenteIa devem ser maiores que zero.")
    .Validate(x => !x.Enabled || string.Equals(x.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase),
        "AssistenteIa:Provider não é suportado.")
    .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.Model),
        "AssistenteIa:Model é obrigatório quando o assistente está habilitado.")
    .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.ApiKey),
        "AssistenteIa:ApiKey é obrigatória quando o assistente está habilitado.")
    .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _),
        "AssistenteIa:BaseUrl deve ser uma URL absoluta.")
    .Validate(x => !string.IsNullOrWhiteSpace(x.TransparencyNoticeVersion),
        "AssistenteIa:TransparencyNoticeVersion é obrigatória.")
    .Validate(x => string.IsNullOrWhiteSpace(x.PrivacyPolicyUrl) ||
                   Uri.TryCreate(x.PrivacyPolicyUrl, UriKind.Absolute, out _),
        "AssistenteIa:PrivacyPolicyUrl deve ser uma URL absoluta quando informada.")
    .ValidateOnStart();
builder.Services.AddOptions<AuthSettings>().Bind(builder.Configuration.GetSection("Auth"))
    .Validate(x => x.JwtSecret.Length >= 32, "Auth:JwtSecret deve possuir ao menos 32 caracteres.")
    .Validate(x => x.AccessTokenTtlMinutes > 0 && x.RefreshTokenTtlDays > 0, "TTLs de autenticacao invalidos.")
    .ValidateOnStart();
builder.Services.AddDbContext<JuridicoAiDbContext>(options =>
    options.UseNpgsql(builder.Configuration["Postgres:ConnectionString"]));
builder.Services.AddHostedService<DatabaseMigrationService>();
var maxFileSize = builder.Configuration.GetValue<long?>("DocumentStorage:MaxFileSizeBytes") ?? 20 * 1024 * 1024;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxFileSize + 1024 * 1024);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JuridicoAI API",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Informe o access token JWT."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                return uri.Host is "localhost" or "127.0.0.1";
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
var jwtSettings = builder.Configuration.GetSection("Auth").Get<AuthSettings>() ?? new AuthSettings();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.JwtSecret)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub,
        RoleClaimType = "role"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var sessionClaim = context.Principal?.FindFirst("sessionId")?.Value;
            var userClaim = context.Principal?.FindFirst("sub")?.Value;
            var organizationClaim = context.Principal?.FindFirst("organizationId")?.Value;
            if (!Guid.TryParse(sessionClaim, out var sessionId) || !Guid.TryParse(userClaim, out var userId) ||
                !Guid.TryParse(organizationClaim, out var organizationId))
            { context.Fail("Sessao invalida."); return; }
            var repository = context.HttpContext.RequestServices.GetRequiredService<IAuthRepository>();
            var session = await repository.ObterSessionAsync(sessionId, context.HttpContext.RequestAborted);
            var user = await repository.ObterUserAsync(userId, context.HttpContext.RequestAborted);
            var membership = await repository.ObterMembershipAsync(userId, organizationId, context.HttpContext.RequestAborted);
            if (session is null || session.UserId != userId || session.OrganizationId != organizationId ||
                session.RevogadaEm is not null || session.ExpiraEm <= DateTime.UtcNow ||
                user?.Status != UserStatusEnum.Active || membership?.Organization.Status != OrganizationStatusEnum.Active ||
                membership.Membership.Role.ToString() != context.Principal?.FindFirst("role")?.Value)
                context.Fail("Sessao invalida.");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ScraperService>();
builder.Services.AddScoped<JurisprudenciaNormalizerService>();
builder.Services.AddScoped<JurisprudenciaClassifierService>();
builder.Services.AddScoped<JurisprudenciaIngestionService>();
builder.Services.AddScoped<CasoApplicationService>();
builder.Services.AddScoped<PecaApplicationService>();
builder.Services.AddScoped<TemplatePecaApplicationService>();
builder.Services.AddScoped<AuthApplicationService>();
builder.Services.AddScoped<OrganizationApplicationService>();
builder.Services.AddScoped<AssistenteIaApplicationService>();
builder.Services.AddScoped<IAssistenteTransparencyService, AssistenteTransparencyService>();
builder.Services.AddScoped<AssistenteContextBuilder>();
builder.Services.AddSingleton<AssistentePromptPolicy>();
builder.Services.AddSingleton<AssistenteRequestRegistry>();
builder.Services.AddScoped<IAssistenteFeatureAccess, AssistenteFeatureAccessService>();
builder.Services.AddSingleton<IAssistenteUsageLimiter, AssistenteUsageLimiter>();
builder.Services.AddScoped<IAssistenteUsageRecorder, AssistenteUsageRecorder>();
builder.Services.AddScoped<JurisprudenciaRawRepository>();
builder.Services.AddScoped<JurisprudenciaRepository>();
builder.Services.AddScoped<ICasoRepository, CasoRepository>();
builder.Services.AddScoped<IPecaRepository, PecaRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IOrganizationFeatureRepository, OrganizationFeatureRepository>();
builder.Services.AddScoped<IAiUsageLedgerRepository, AiUsageLedgerRepository>();
builder.Services.AddScoped<IAiTransparencyAcknowledgementRepository, AiTransparencyAcknowledgementRepository>();
builder.Services.AddScoped<IAuthContext, HttpAuthContext>();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddHttpClient<OpenAiAssistenteIaProvider>((services, client) =>
{
    var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AssistenteIaSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = Timeout.InfiniteTimeSpan;
}).RemoveAllLoggers();
builder.Services.AddScoped<IAssistenteIaProvider>(services =>
    services.GetRequiredService<OpenAiAssistenteIaProvider>());
builder.Services.AddScoped<IAssistenteTokenCounter>(services =>
    services.GetRequiredService<OpenAiAssistenteIaProvider>());
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "JuridicoAI API v1");
        options.DocumentTitle = "JuridicoAI API";
    });
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseMiddleware<AssistenteRequestSizeMiddleware>();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapGet("/", () => "Hello World!");
app.MapControllers();
if (args.Contains("provision-organization", StringComparer.OrdinalIgnoreCase))
{
    var request = new ProvisionOrganizationRequest(
        Environment.GetEnvironmentVariable("PROVISION_ORGANIZATION_NAME") ?? string.Empty,
        Environment.GetEnvironmentVariable("PROVISION_OWNER_NAME") ?? string.Empty,
        Environment.GetEnvironmentVariable("PROVISION_OWNER_EMAIL") ?? string.Empty,
        Environment.GetEnvironmentVariable("PROVISION_OWNER_PASSWORD") ?? string.Empty);
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
    await db.Database.MigrateAsync();
    var result = await scope.ServiceProvider.GetRequiredService<OrganizationApplicationService>().ProvisionarAsync(request, default);
    Console.WriteLine($"Organization provisionada: {result.Organization.Id}; owner: {result.User.Id}");
    return;
}
if (args.Contains("set-ai-feature", StringComparer.OrdinalIgnoreCase))
{
    var organizationText = Environment.GetEnvironmentVariable("AI_FEATURE_ORGANIZATION_ID");
    var featureText = Environment.GetEnvironmentVariable("AI_FEATURE_NAME");
    var enabledText = Environment.GetEnvironmentVariable("AI_FEATURE_ENABLED");
    if (!Guid.TryParse(organizationText, out var organizationId) ||
        !Enum.TryParse<OrganizationFeatureEnum>(featureText, ignoreCase: true, out var feature) ||
        !Enum.IsDefined(feature) ||
        !bool.TryParse(enabledText, out var enabled))
        throw new InvalidOperationException(
            "Informe AI_FEATURE_ORGANIZATION_ID, AI_FEATURE_NAME e AI_FEATURE_ENABLED válidos.");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IOrganizationFeatureRepository>().DefinirAsync(
        organizationId,
        feature,
        enabled,
        updatedById: null,
        DateTime.UtcNow,
        default);
    Console.WriteLine($"Feature {feature} definida como {enabled} para a organização {organizationId}.");
    return;
}

app.Run();
