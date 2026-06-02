using JuridicoAI.Application.Services;
using JuridicoAI.Infrastructure.Repositories;
using Microsoft.OpenApi;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<PeticaoService>();
builder.Services.AddScoped<TJSPJurisprudenciaRepository>();
builder.Services.AddScoped<IJurisprudenciaProvider>(provider =>
    provider.GetRequiredService<TJSPJurisprudenciaRepository>());
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JuridicoAI API",
        Version = "v1",
        Description = "API para pesquisa de jurisprudências e geração de rascunhos técnicos de petições iniciais com IA."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "JuridicoAI API v1");
        options.DocumentTitle = "JuridicoAI Swagger";
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
