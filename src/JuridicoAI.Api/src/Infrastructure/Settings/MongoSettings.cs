namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class MongoSettings
{
    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = "JuridicoAi";
    public string RawCollectionName { get; init; } = "jurisprudencias_raw";
    public string JurisprudenciasCollectionName { get; init; } = "jurisprudencias";
    public int ServerSelectionTimeoutSeconds { get; init; } = 10;
}
