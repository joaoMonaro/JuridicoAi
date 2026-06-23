namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class MongoSettings
{
    public string ConnectionString { get; init; } = "mongodb://admin:123@localhost:27017/";
    public string DatabaseName { get; init; } = "JuridicoAi";
    public string CollectionName { get; init; } = "tjsp_search_results";
}
