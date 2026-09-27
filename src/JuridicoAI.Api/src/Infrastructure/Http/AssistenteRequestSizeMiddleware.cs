using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.Http;

public sealed class AssistenteRequestSizeMiddleware(
    RequestDelegate next,
    IOptionsMonitor<AssistenteIaSettings> options)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.Value?.Contains("/assistente/solicitacoes", StringComparison.OrdinalIgnoreCase) == true)
        {
            var limit = options.CurrentValue.MaxRequestBodyBytes;
            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = limit;
            if (context.Request.ContentLength > limit)
                throw new BadHttpRequestException("Payload excede o limite permitido.", StatusCodes.Status413PayloadTooLarge);
        }

        return next(context);
    }
}
