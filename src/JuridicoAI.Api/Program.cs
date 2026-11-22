using JuridicoAI.Api.Infrastructure.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;
using System.Text.Json.Serialization;
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
using JuridicoAI.Api.Infrastructure.Export;
using JuridicoAI.Api.Infrastructure.Storage;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Documents;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IAssistenteTelemetry>(AssistenteTelemetry.Shared);
builder.Services.AddOptions<AssistenteMonitoringSettings>().Bind(builder.Configuration.GetSection("AssistenteMonitoring"))
    .Validate(x => x.CollectionIntervalSeconds is >= 10 and <= 3600 && x.QueryTimeoutSeconds is >= 1 and <= 30 &&
        x.StalledJobSeconds >= 60 && x.PendingDeletionSeconds >= 60 && x.OldReservationSeconds >= 60,
        "Limites de monitoramento inválidos.")
    .Validate(x => !x.Enabled || Uri.TryCreate(x.OtlpEndpoint, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query),
        "Endpoint OTLP inválido.")
    .ValidateOnStart();
builder.Services.AddSingleton<AssistenteMonitoringState>();
builder.Services.AddScoped<AssistenteMonitoringCollector>();
if (builder.Configuration.GetValue<bool>("AssistenteMonitoring:Enabled"))
{
    builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
    builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("juridicoai-api"))
        .WithMetrics(metrics => metrics.AddMeter(AssistenteTelemetry.MeterName)
            .AddView("ai.stage.duration", new ExplicitBucketHistogramConfiguration { Boundaries = [.05, .1, .25, .5, 1, 2, 5, 10, 20, 30, 60, 90, 120, 180, 300, 600, 900] })
            .AddView("ai.provider.call.duration", new ExplicitBucketHistogramConfiguration { Boundaries = [.05, .1, .25, .5, 1, 2, 5, 10, 20, 30, 60, 90, 120, 180, 300, 600, 900] })
            .AddOtlpExporter((exporter, reader) =>
        {
            exporter.Endpoint = new Uri(builder.Configuration["AssistenteMonitoring:OtlpEndpoint"] ?? "http://localhost:4318/v1/metrics");
            exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
            exporter.TimeoutMilliseconds = 5000;
            reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 15000;
        }));
}


builder.Services.Configure<DocumentStorageSettings>(builder.Configuration.GetSection("DocumentStorage"));
builder.Services.AddOptions<AssistenteDocumentSettings>().Bind(builder.Configuration.GetSection("AssistenteDocumentos"))
    .Validate(x => !x.AnalysisEnabled || x.RetrievalEnabled, "Análise extensa exige recuperação documental.")
    .Validate(x => !x.AnalysisEnabled || x.MaxAnalysisCalls is >= 3 and <= 30 && x.MaxAnalysisTotalTokens > 0 &&
        x.AnalysisBatchCharacters is >= 8000 and <= 32000 && x.AnalysisSummaryCharacters is >= 2000 and <= 12000 &&
        x.AnalysisOutputTokens is >= 500 and <= 6000 && x.AnalysisDeadlineSeconds >= x.AnalysisLeaseSeconds &&
        x.AnalysisLeaseSeconds > builder.Configuration.GetValue<int>("AssistenteIa:TimeoutSeconds", 90) + 30,
        "Limites da análise documental inválidos.")
    .Validate(x => !x.RetrievalEnabled || x.Enabled, "Recuperação documental exige documentos habilitados.")
    .Validate(x => x.MaxRetrievalRounds is >= 1 and <= 12 && x.MaxRetrievalTotalTokens > 0 &&
        x.MaxRetrievalResultCharacters is >= 4000 and <= 16000, "Limites de recuperação documental inválidos.")
    .Validate(x => !x.Enabled || builder.Configuration.GetValue<bool>("AssistenteIa:ConversationV2Enabled") &&
        builder.Configuration.GetValue<bool>("AssistenteIa:ReviewV2Enabled"), "Documentos exigem conversa e revisão persistidas.")
    .Validate(x => !x.OcrEnabled || !string.IsNullOrWhiteSpace(x.OcrCalibrationVersion), "OCR exige uma versão de calibração.")
    .Validate(x => !x.OcrEnabled || Path.IsPathRooted(x.TessdataPath) && File.Exists(Path.Combine(x.TessdataPath, "por.traineddata")) &&
        File.Exists(Path.Combine(x.TessdataPath, "eng.traineddata")), "OCR exige TessdataPath absoluto com modelos por e eng.")
    .Validate(x => x.MaxFileBytes is > 0 and <= 20 * 1024 * 1024 && x.MaxAttachments > 0 && x.MaxConversationBytes > 0 &&
        x.MaxPages > 0 && x.MaxPixels > 0 && x.MaxExtractedCharacters > 0 && x.ProcessTimeoutSeconds > 0 &&
        x.LeaseSeconds > x.ProcessTimeoutSeconds * 3 && x.MaxAttempts > 0, "Limites documentais inválidos.")
    .ValidateOnStart();
builder.Services.AddOptions<AssistenteIaSettings>().Bind(builder.Configuration.GetSection("AssistenteIa"))
    .Validate(x => !x.ConversationV2Enabled || x.ReviewV2Enabled,
        "AssistenteIa:ConversationV2Enabled exige ReviewV2Enabled.")
    .Validate(x => !x.DurableGenerationEnabled || x.ConversationV2Enabled && x.ReviewV2Enabled && x.GenerationMaxSections is >= 1 and <= 8 &&
        x.GenerationMaxCalls is >= 3 and <= 30 && x.GenerationMaxTotalTokens > 0 && x.GenerationMaxDocumentCharacters is >= 20000 and <= 120000 &&
        x.GenerationSectionCharacters is >= 2000 and <= 24000 && x.GenerationLeaseSeconds > x.TimeoutSeconds + 30 &&
        x.GenerationDeadlineSeconds >= x.GenerationLeaseSeconds, "Limites de geração durável inválidos.")
    .Validate(x => !x.HistoryMemoryEnabled || x.ConversationV2Enabled && x.ReviewV2Enabled, "Memória exige conversa e revisão persistidas.")
    .Validate(x => x.HistoryCompactionMessages >= x.HistoryRecentMessages && x.HistoryCompactionMessages <= 200 && x.HistoryRecentMessages is >= 2 and <= 100 && x.HistoryRecentCharacters is >= 4000 and <= 24000 &&
        x.HistoryBatchCharacters is >= 4000 and <= 32000 && x.HistoryMemoryCharacters is >= 4000 and <= 16000 &&
        x.HistoryToolRounds is >= 2 and <= 12 && x.HistoryTotalTokens > 0, "Limites da memória inválidos.")
    .Validate(x => x.TimeoutSeconds > 0 && x.MaxInstructionCharacters > 0 &&
                   x.MaxDocumentContextCharacters > 0 && x.MaxAdditionalContextCharacters > 0 &&
                   x.MaxTotalContextCharacters > 0 && x.MaxInputTokens > 0 && x.MaxOutputCharacters > 0 &&
                   x.MaxOutputTokens > 0 && x.MaxRequestBodyBytes > 0 &&
                   x.RequestsPerUserPerMinute > 0 && x.ConcurrentRequestsPerUser > 0 &&
                   x.RequestsPerOrganizationPerMinute > 0 && x.ConcurrentRequestsPerOrganization > 0 &&
                   x.InputCostPerMillionTokensUsd >= 0 && x.CachedInputCostPerMillionTokensUsd >= 0 &&
                   x.OutputCostPerMillionTokensUsd >= 0 && x.DefaultMonthlyLimitUsd >= 0 && !string.IsNullOrWhiteSpace(x.PricingVersion),
        "Os limites de AssistenteIa devem ser maiores que zero.")
    .Validate(x => !(builder.Environment.IsProduction() && x.AllowSimulatedWithoutFinancialControl),
        "AssistenteIa:AllowSimulatedWithoutFinancialControl não pode ser habilitado em produção.")
    .Validate(x => !builder.Environment.IsProduction() || !x.Enabled ||
                   (x.DefaultMonthlyLimitUsd > 0 &&
                    x.InputCostPerMillionTokensUsd > 0 &&
                    x.CachedInputCostPerMillionTokensUsd > 0 &&
                    x.OutputCostPerMillionTokensUsd > 0 &&
                    !string.IsNullOrWhiteSpace(x.PricingVersion) &&
                    !string.Equals(x.PricingVersion.Trim(), "unconfigured", StringComparison.OrdinalIgnoreCase)),
        "Produção com IA habilitada exige preços válidos, versão de tarifação e limite mensal configurado.")
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
builder.Services.AddSingleton<IValidateOptions<AuthSettings>, AuthSettingsValidator>();
builder.Services.AddOptions<AuthSettings>().Bind(builder.Configuration.GetSection("Auth"))
    .ValidateOnStart();
builder.Services.AddOptions<ForwardedHeadersOptions>().PostConfigure<IConfiguration>(
    (options, configuration) => (configuration.GetSection("ReverseProxy").Get<ReverseProxySettings>() ?? new())
        .ConfigureForwardedHeaders(options));
builder.Services.AddDbContext<JuridicoAiDbContext>(options =>
    options.UseNpgsql(builder.Configuration["Postgres:ConnectionString"]));
builder.Services.AddHostedService<DatabaseMigrationService>();
builder.Services.AddHostedService<AssistenteMonitoringWorker>();
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
builder.Services.AddSingleton<FrontendOriginPolicy>();
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<FrontendOriginPolicy>(
    (options, origins) => origins.ConfigureCors(options));
var jwtSettings = builder.Configuration.GetSection("Auth").Get<AuthSettings>() ?? new AuthSettings();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => OrganizationAuthentication.Configure(options, jwtSettings));
builder.Services.AddAuthentication().AddJwtBearer(PersonalHistoryAuthentication.Scheme, options => PersonalHistoryAuthentication.Configure(options, jwtSettings));
builder.Services.AddScoped<IPersonalHistoryContext, PersonalHistoryContext>();
builder.Services.AddScoped<PersonalHistoryAuthService>();
builder.Services.AddScoped<PersonalHistoryService>();
builder.Services.AddScoped<AssistenteDeletionProcessor>();
builder.Services.AddHostedService<AssistenteDeletionWorker>();
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(AuthRateLimitPolicy.Configure);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CasoApplicationService>();
builder.Services.AddScoped<PecaApplicationService>();
builder.Services.Configure<PecaExportSettings>(builder.Configuration.GetSection("PecaExport"));
builder.Services.AddSingleton<PecaDocxGenerator>();
builder.Services.AddSingleton<IPecaPdfConverter, LibreOfficePdfConverter>();
builder.Services.AddScoped<PecaExportApplicationService>();
builder.Services.AddScoped<TemplatePecaApplicationService>();
builder.Services.AddScoped<AuthApplicationService>();
builder.Services.AddScoped<OrganizationApplicationService>();
builder.Services.AddScoped<AssistenteIaApplicationService>();
builder.Services.AddScoped<AssistenteWorkspaceService>();
builder.Services.AddScoped<AssistenteChatService>();
builder.Services.AddScoped<AssistenteHistoryService>();
builder.Services.AddScoped<AssistenteDocumentService>();
builder.Services.AddScoped<AssistenteDocumentProcessor>();
builder.Services.AddScoped<AssistenteDocumentAnalysisService>();
builder.Services.AddScoped<AssistenteGenerationService>();
builder.Services.AddScoped<AssistenteGenerationProcessor>();
builder.Services.AddHostedService<AssistenteGenerationWorker>();
builder.Services.AddScoped<AssistenteDocumentAnalysisProcessor>();
builder.Services.AddHostedService<AssistenteDocumentAnalysisWorker>();
builder.Services.AddSingleton<DocumentProcess>();
builder.Services.AddSingleton<LocalDocumentExtractor>();
builder.Services.AddHostedService<AssistenteDocumentWorker>();
builder.Services.AddSingleton<AssistentePatchCompiler>();
builder.Services.AddScoped<IAssistenteWorkspaceRepository, AssistenteWorkspaceRepository>();
builder.Services.AddScoped<IAssistenteTransparencyService, AssistenteTransparencyService>();
builder.Services.AddScoped<AssistenteContextBuilder>();
builder.Services.AddSingleton<AssistentePromptPolicy>();
builder.Services.AddSingleton<AssistenteRequestRegistry>();
builder.Services.AddScoped<IAssistenteFeatureAccess, AssistenteFeatureAccessService>();
builder.Services.AddSingleton<IAssistenteUsageLimiter, AssistenteUsageLimiter>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAssistenteMonthlyLimitService, AssistenteMonthlyLimitService>();
builder.Services.AddScoped<IAssistenteUsageRecorder, AssistenteUsageRecorder>();
builder.Services.AddScoped<ICasoRepository, CasoRepository>();
builder.Services.AddScoped<IPecaRepository, PecaRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IOrganizationFeatureRepository, OrganizationFeatureRepository>();
builder.Services.AddScoped<IAiOperationalControlRepository, AiOperationalControlRepository>();
builder.Services.AddScoped<IAiUsageLedgerRepository, AiUsageLedgerRepository>();
builder.Services.AddScoped<IAiTransparencyAcknowledgementRepository, AiTransparencyAcknowledgementRepository>();
builder.Services.AddScoped<HttpAuthContext>();
builder.Services.AddScoped<AssistenteBackgroundIdentity>();
builder.Services.AddScoped<IAuthContext, AssistenteExecutionAuthContext>();
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

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseMiddleware<ProductionHttpsMiddleware>();
app.UseMiddleware<AssistenteRequestSizeMiddleware>();
app.UseCors(FrontendOriginPolicy.PolicyName);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapGet("/", () => "Hello World!");
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.MapControllers();
if (args.Contains("migrate-database", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
    await db.Database.MigrateAsync();
    Console.WriteLine("Migrations aplicadas.");
    return;
}
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
    var updatedByText = Environment.GetEnvironmentVariable("AI_OPERATION_UPDATED_BY_ID");
    var reason = Environment.GetEnvironmentVariable("AI_OPERATION_REASON");
    if (!Guid.TryParse(organizationText, out var organizationId) ||
        !Enum.TryParse<OrganizationFeatureEnum>(featureText, ignoreCase: true, out var feature) ||
        !Enum.IsDefined(feature) ||
        !bool.TryParse(enabledText, out var enabled) ||
        !Guid.TryParse(updatedByText, out var updatedById) ||
        string.IsNullOrWhiteSpace(reason))
        throw new InvalidOperationException(
            "Informe AI_FEATURE_ORGANIZATION_ID, AI_FEATURE_NAME, AI_FEATURE_ENABLED, " +
            "AI_OPERATION_UPDATED_BY_ID e AI_OPERATION_REASON válidos.");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IOrganizationFeatureRepository>().DefinirAsync(
        organizationId,
        feature,
        enabled,
        updatedById,
        reason,
        DateTime.UtcNow,
        default);
    Console.WriteLine($"Feature {feature} definida como {enabled} para a organização {organizationId}.");
    return;
}
if (args.Contains("set-ai-enabled", StringComparer.OrdinalIgnoreCase))
{
    var enabledText = Environment.GetEnvironmentVariable("AI_ENABLED");
    var updatedByText = Environment.GetEnvironmentVariable("AI_OPERATION_UPDATED_BY_ID");
    var reason = Environment.GetEnvironmentVariable("AI_OPERATION_REASON");
    if (!bool.TryParse(enabledText, out var enabled) ||
        !Guid.TryParse(updatedByText, out var updatedById) ||
        string.IsNullOrWhiteSpace(reason))
        throw new InvalidOperationException(
            "Informe AI_ENABLED, AI_OPERATION_UPDATED_BY_ID e AI_OPERATION_REASON válidos.");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IAiOperationalControlRepository>()
        .DefinirAssistenteHabilitadoAsync(
            enabled,
            updatedById,
            reason,
            DateTime.UtcNow,
            default);
    Console.WriteLine($"Kill switch AI_ENABLED definido como {enabled}.");
    return;
}
if (args.Contains("set-ai-monthly-limit", StringComparer.OrdinalIgnoreCase))
{
    var orgText = Environment.GetEnvironmentVariable("AI_LIMIT_ORGANIZATION_ID");
    var limitText = Environment.GetEnvironmentVariable("AI_LIMIT_USD");
    var updatedByText = Environment.GetEnvironmentVariable("AI_OPERATION_UPDATED_BY_ID");
    var reason = Environment.GetEnvironmentVariable("AI_OPERATION_REASON");
    if (!Guid.TryParse(orgText, out var organizationId) ||
        !decimal.TryParse(limitText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var limitUsd) ||
        limitUsd < 0m ||
        !Guid.TryParse(updatedByText, out var updatedById) ||
        string.IsNullOrWhiteSpace(reason))
        throw new InvalidOperationException(
            "Informe AI_LIMIT_ORGANIZATION_ID, AI_LIMIT_USD (>= 0), AI_OPERATION_UPDATED_BY_ID e AI_OPERATION_REASON válidos.");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IAssistenteMonthlyLimitService>()
        .DefinirLimiteOrganizacaoAsync(organizationId, limitUsd, updatedById, reason, default);
    Console.WriteLine($"Limite mensal da organização {organizationId} atualizado com auditoria.");
    return;
}
if (args.Contains("reconcile-ai-reservation", StringComparer.OrdinalIgnoreCase))
{
    var resText = Environment.GetEnvironmentVariable("AI_RESERVATION_ID");
    var actionText = Environment.GetEnvironmentVariable("AI_RECONCILE_ACTION");
    var updatedByText = Environment.GetEnvironmentVariable("AI_OPERATION_UPDATED_BY_ID");
    var reason = Environment.GetEnvironmentVariable("AI_OPERATION_REASON");
    if (!Guid.TryParse(resText, out var reservationId) ||
        string.IsNullOrWhiteSpace(actionText) ||
        !Guid.TryParse(updatedByText, out var operatorUserId) ||
        string.IsNullOrWhiteSpace(reason))
        throw new InvalidOperationException(
            "Informe AI_RESERVATION_ID, AI_RECONCILE_ACTION (settle|release), AI_OPERATION_UPDATED_BY_ID e AI_OPERATION_REASON válidos.");

    bool confirmarConsumo;
    if (string.Equals(actionText, "settle", StringComparison.OrdinalIgnoreCase)) confirmarConsumo = true;
    else if (string.Equals(actionText, "release", StringComparison.OrdinalIgnoreCase)) confirmarConsumo = false;
    else throw new InvalidOperationException("AI_RECONCILE_ACTION deve ser 'settle' ou 'release'.");

    int? inputTokens = int.TryParse(Environment.GetEnvironmentVariable("AI_RECONCILE_INPUT_TOKENS"), out var inVal) ? inVal : null;
    int? cachedTokens = int.TryParse(Environment.GetEnvironmentVariable("AI_RECONCILE_CACHED_INPUT_TOKENS"), out var cachedVal) ? cachedVal : null;
    int? outputTokens = int.TryParse(Environment.GetEnvironmentVariable("AI_RECONCILE_OUTPUT_TOKENS"), out var outVal) ? outVal : null;

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
    await db.Database.MigrateAsync();
    var reconciled = await scope.ServiceProvider.GetRequiredService<IAssistenteMonthlyLimitService>()
        .ReconciliarIndeterminadaAsync(reservationId, confirmarConsumo, inputTokens, cachedTokens, outputTokens, operatorUserId, reason, default);
    Console.WriteLine($"Reserva {reconciled.ReservationId} reconciliada para {reconciled.Status}.");
    return;
}
if (args.Contains("recover-orphan-ai-reservation", StringComparer.OrdinalIgnoreCase))
{
    var resText = Environment.GetEnvironmentVariable("AI_RESERVATION_ID");
    var actionText = Environment.GetEnvironmentVariable("AI_ORPHAN_ACTION");
    var drainedText = Environment.GetEnvironmentVariable("AI_CONFIRM_WORKERS_DRAINED");
    var drainEvidence = Environment.GetEnvironmentVariable("AI_DRAIN_EVIDENCE");
    var updatedByText = Environment.GetEnvironmentVariable("AI_OPERATION_UPDATED_BY_ID");
    var reason = Environment.GetEnvironmentVariable("AI_OPERATION_REASON");
    if (!Guid.TryParse(resText, out var reservationId) ||
        string.IsNullOrWhiteSpace(actionText) ||
        !bool.TryParse(drainedText, out var confirmWorkersDrained) ||
        !confirmWorkersDrained ||
        string.IsNullOrWhiteSpace(drainEvidence) ||
        !Guid.TryParse(updatedByText, out var operatorUserId) ||
        string.IsNullOrWhiteSpace(reason))
        throw new InvalidOperationException(
            "Informe AI_RESERVATION_ID, AI_ORPHAN_ACTION (mark-indeterminate|settle|release), AI_CONFIRM_WORKERS_DRAINED=true, AI_DRAIN_EVIDENCE, AI_OPERATION_UPDATED_BY_ID e AI_OPERATION_REASON válidos.");

    var action = actionText.Trim().ToLowerInvariant() switch
    {
        "mark-indeterminate" => AssistenteOrphanRecoveryAction.MarkIndeterminate,
        "settle" => AssistenteOrphanRecoveryAction.Settle,
        "release" => AssistenteOrphanRecoveryAction.Release,
        _ => throw new InvalidOperationException("AI_ORPHAN_ACTION deve ser 'mark-indeterminate', 'settle' ou 'release'.")
    };

    int? inputTokens = int.TryParse(Environment.GetEnvironmentVariable("AI_RECONCILE_INPUT_TOKENS"), out var inVal) ? inVal : null;
    int? cachedTokens = int.TryParse(Environment.GetEnvironmentVariable("AI_RECONCILE_CACHED_INPUT_TOKENS"), out var cachedVal) ? cachedVal : null;
    int? outputTokens = int.TryParse(Environment.GetEnvironmentVariable("AI_RECONCILE_OUTPUT_TOKENS"), out var outVal) ? outVal : null;

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
    await db.Database.MigrateAsync();
    var recovered = await scope.ServiceProvider.GetRequiredService<IAssistenteMonthlyLimitService>()
        .RecuperarReservaOrfaAsync(reservationId, action, confirmWorkersDrained, drainEvidence, inputTokens, cachedTokens, outputTokens, operatorUserId, reason, default);
    Console.WriteLine($"Reserva órfã {recovered.ReservationId} recuperada para {recovered.Status}.");
    return;
}

app.Run();
