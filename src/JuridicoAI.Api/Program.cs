using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Gateways.OpenIA;
using JuridicoAI.Api.Infrastructure.Http;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using JuridicoAI.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "LocalFrontend";

builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<PostgresSettings>(builder.Configuration.GetSection("Postgres"));
builder.Services.Configure<ScraperSettings>(builder.Configuration.GetSection("Scraper"));
builder.Services.Configure<DocumentStorageSettings>(builder.Configuration.GetSection("DocumentStorage"));
builder.Services.AddDbContext<JuridicoAiDbContext>(options =>
    options.UseNpgsql(builder.Configuration["Postgres:ConnectionString"]));
builder.Services.AddHostedService<DatabaseMigrationService>();
var maxFileSize = builder.Configuration.GetValue<long?>("DocumentStorage:MaxFileSizeBytes") ?? 20 * 1024 * 1024;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxFileSize + 1024 * 1024);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
            .AllowAnyMethod();
    });
});
builder.Services.AddScoped<PeticaoApplicationService>();
builder.Services.AddScoped<ScraperService>();
builder.Services.AddScoped<JurisprudenciaNormalizerService>();
builder.Services.AddScoped<JurisprudenciaClassifierService>();
builder.Services.AddScoped<JurisprudenciaIngestionService>();
builder.Services.AddScoped<CasoApplicationService>();
builder.Services.AddScoped<PecaApplicationService>();
builder.Services.AddScoped<TemplatePecaApplicationService>();
builder.Services.AddScoped<JurisprudenciaRawRepository>();
builder.Services.AddScoped<JurisprudenciaRepository>();
builder.Services.AddScoped<ICasoRepository, CasoRepository>();
builder.Services.AddScoped<IPecaRepository, PecaRepository>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddHttpClient<OpenIAGateway>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
});

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors(CorsPolicy);
app.MapGet("/", () => "Hello World!");
app.MapControllers();
app.Run();
