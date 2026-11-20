namespace JuridicoAI.Api.Infrastructure.Http;

public sealed class ProductionHttpsMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (!environment.IsProduction() || context.Request.IsHttps)
            return next(context);

        // Apenas o esquema da conexão é autoridade. Cabeçalhos de proxy exigem
        // tratamento anterior por middleware configurado com proxies confiáveis.
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "HTTPS obrigatório",
            detail: "Use HTTPS para acessar a API em produção.",
            extensions: new Dictionary<string, object?> { ["code"] = "HTTPS_REQUIRED" })
            .ExecuteAsync(context);
    }
}
