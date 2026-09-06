namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class AuthSettings
{
    public string JwtSecret { get; init; } = string.Empty;
    public string Issuer { get; init; } = "JuridicoAI";
    public string Audience { get; init; } = "JuridicoAI.Web";
    public int AccessTokenTtlMinutes { get; init; } = 15;
    public int RefreshTokenTtlDays { get; init; } = 30;
    public bool SecureRefreshCookie { get; init; }
}
