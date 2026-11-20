using Microsoft.AspNetCore.Cors.Infrastructure;

namespace JuridicoAI.Api.Infrastructure.Http;

public sealed class FrontendOriginPolicy
{
    public const string PolicyName = "Frontend";
    private readonly HashSet<string> _allowedOrigins;
    private readonly bool _development;

    public FrontendOriginPolicy(IConfiguration configuration, IHostEnvironment environment)
    {
        _development = environment.IsDevelopment();
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (environment.IsProduction() && origins.Length == 0)
            throw new InvalidOperationException("Cors:AllowedOrigins exige ao menos uma origem HTTPS explícita em produção.");

        for (var i = 0; i < origins.Length; i++)
        {
            var uri = ParseOrigin(origins[i]);
            if (uri is null || environment.IsProduction() && (uri.Scheme != "https" || uri.IsLoopback))
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins[{i}] deve ser uma origem HTTP/HTTPS exata, sem caminho, barra final, credenciais ou curingas; produção exige HTTPS e host não local.");
        }

        _allowedOrigins = new HashSet<string>(origins, StringComparer.Ordinal);
    }

    public bool IsAllowed(string? origin)
    {
        var uri = ParseOrigin(origin);
        return uri is not null && (_allowedOrigins.Contains(origin!) || _development && uri.IsLoopback);
    }

    public void ConfigureCors(CorsOptions options) =>
        options.AddPolicy(PolicyName, policy => policy
            .SetIsOriginAllowed(IsAllowed)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition")
            .AllowCredentials());

    private static Uri? ParseOrigin(string? origin)
    {
        if (string.IsNullOrEmpty(origin) || origin.Contains('*') ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal))
            return null;

        return uri;
    }
}
