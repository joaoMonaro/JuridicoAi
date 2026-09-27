namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class AssistenteIaSettings
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public int TimeoutSeconds { get; set; } = 90;
    public int MaxInstructionCharacters { get; set; } = 4_000;
    public int MaxDocumentContextCharacters { get; set; } = 60_000;
    public int MaxAdditionalContextCharacters { get; set; } = 8_000;
    public int MaxTotalContextCharacters { get; set; } = 80_000;
    public int MaxOutputCharacters { get; set; } = 20_000;
    public int MaxOutputTokens { get; set; } = 6_000;
    public int RequestsPerUserPerMinute { get; set; } = 6;
}
