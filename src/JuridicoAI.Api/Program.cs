using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Gateways.OpenIA;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "LocalFrontend";

builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<PostgresSettings>(builder.Configuration.GetSection("Postgres"));
builder.Services.Configure<ScraperSettings>(builder.Configuration.GetSection("Scraper"));
builder.Services.AddSingleton<PostgresConnectionFactory>();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

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
builder.Services.AddScoped<JurisprudenciaRawRepository>();
builder.Services.AddScoped<JurisprudenciaRepository>();
builder.Services.AddHttpClient<OpenIAGateway>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
});

var app = builder.Build();

app.UseCors(CorsPolicy);

app.MapGet("/", () => "Hello World!");
app.MapControllers();

app.Run();
