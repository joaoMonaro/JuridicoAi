using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Gateways.OpenIA;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<PeticaoApplicationService>();
builder.Services.AddHttpClient<OpenIAGateway>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapControllers();

app.Run();
