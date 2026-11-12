namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class AssistenteIaSettings
{
    public bool Enabled { get; set; }
    public bool ReviewV2Enabled { get; set; }
    public bool ConversationV2Enabled { get; set; }
    public bool AutomaticConversationEnabled { get; set; }
    public bool HistoryMemoryEnabled { get; set; }
    public int HistoryCompactionMessages { get; set; } = 64;
    public int HistoryRecentMessages { get; set; } = 16;
    public int HistoryRecentCharacters { get; set; } = 16_000;
    public int HistoryBatchCharacters { get; set; } = 24_000;
    public int HistoryMemoryCharacters { get; set; } = 12_000;
    public int HistoryToolRounds { get; set; } = 8;
    public int HistoryTotalTokens { get; set; } = 180_000;
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public int TimeoutSeconds { get; set; } = 90;
    public int MaxInstructionCharacters { get; set; } = 4_000;
    public int MaxDocumentContextCharacters { get; set; } = 60_000;
    public int MaxAdditionalContextCharacters { get; set; } = 8_000;
    public int MaxTotalContextCharacters { get; set; } = 80_000;
    public int MaxInputTokens { get; set; } = 60_000;
    public int MaxOutputCharacters { get; set; } = 20_000;
    public int MaxOutputTokens { get; set; } = 6_000;
    public long MaxRequestBodyBytes { get; set; } = 512 * 1024;
    public int RequestsPerUserPerMinute { get; set; } = 10;
    public int ConcurrentRequestsPerUser { get; set; } = 2;
    public int RequestsPerOrganizationPerMinute { get; set; } = 30;
    public int ConcurrentRequestsPerOrganization { get; set; } = 6;
    public decimal InputCostPerMillionTokensUsd { get; set; }
    public decimal CachedInputCostPerMillionTokensUsd { get; set; }
    public decimal OutputCostPerMillionTokensUsd { get; set; }
    public string PricingVersion { get; set; } = "unconfigured";
    public string TransparencyNoticeVersion { get; set; } = "ai-transparency-v1";
    public string? PrivacyPolicyUrl { get; set; }
}
