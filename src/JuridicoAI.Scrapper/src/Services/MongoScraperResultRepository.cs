using JuridicoAI.Scrapper.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JuridicoAI.Scrapper.Services;

public sealed class MongoScraperResultRepository
{
    public const string DefaultConnectionString = "mongodb://admin:123@localhost:27017/";
    public const string DefaultDatabaseName = "JuridicoAi";
    public const string DefaultCollectionName = "tjsp_search_results";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoScraperResultRepository(string connectionString, string databaseName, string collectionName)
    {
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<BsonDocument>(collectionName);
    }

    public Task SaveAsync(
        TjspSearchRequest request,
        TjspSearchResult result,
        string resultJson,
        CancellationToken cancellationToken)
    {
        var document = new BsonDocument
        {
            ["createdAtUtc"] = DateTime.UtcNow,
            ["source"] = "TJSP",
            ["termo"] = result.Termo,
            ["tipoDecisao"] = request.TipoDecisao,
            ["incluirSinonimos"] = request.IncluirSinonimos,
            ["quantidadeMaxima"] = request.QuantidadeMaxima,
            ["totalEncontrado"] = result.TotalEncontrado,
            ["quantidadeRetornada"] = result.QuantidadeRetornada,
            ["result"] = BsonDocument.Parse(resultJson)
        };

        return _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }
}
