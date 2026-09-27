using System.Diagnostics;
using JuridicoAI.Api.Application.Abstractions;

namespace JuridicoAI.Api.Infrastructure.Http;

public static class CorrelationHeaders
{
    public const string Name = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";
}

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationHeaders.Name, out var supplied)
                            && Guid.TryParse(supplied.FirstOrDefault(), out var parsed)
            ? parsed
            : Guid.NewGuid();

        context.Items[CorrelationHeaders.ItemKey] = correlationId;
        context.TraceIdentifier = correlationId.ToString("D");
        context.Response.Headers[CorrelationHeaders.Name] = correlationId.ToString("D");
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            await next(context);
    }
}

public sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public Guid CorrelationId => accessor.HttpContext?.Items[CorrelationHeaders.ItemKey] is Guid value
        ? value
        : throw new InvalidOperationException("CorrelationId não foi inicializado.");
}
