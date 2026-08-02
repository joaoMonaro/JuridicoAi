namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class ScraperSettings
{
    public int TimeoutSeconds { get; init; } = 120;
    public int MaxResults { get; init; } = 30;
}
