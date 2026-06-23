using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class TjspSearchResultRepository
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public TjspSearchResultRepository(IOptions<MongoSettings> mongoSettings)
    {
        var settings = mongoSettings.Value;
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<BsonDocument>(settings.CollectionName);
    }

    public async Task<IReadOnlyList<string>> ObterEmentasAsync(CancellationToken cancellationToken = default)
    {
        var projection = Builders<BsonDocument>.Projection
            .Include("result.Resultados.Ementa")
            .Include("result.resultados.ementa");

        var documentos = await _collection
            .Find(Builders<BsonDocument>.Filter.Empty)
            .Project(projection)
            .ToListAsync(cancellationToken);

        return documentos
            .SelectMany(ExtrairEmentas)
            .Where(ementa => !string.IsNullOrWhiteSpace(ementa))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> ExtrairEmentas(BsonDocument documento)
    {
        if (!documento.TryGetValue("result", out var resultValue) || !resultValue.IsBsonDocument)
        {
            yield break;
        }

        var result = resultValue.AsBsonDocument;
        var resultados = ObterArray(result, "Resultados") ?? ObterArray(result, "resultados");
        if (resultados is null)
        {
            yield break;
        }

        foreach (var item in resultados.Where(item => item.IsBsonDocument).Select(item => item.AsBsonDocument))
        {
            var ementa = ObterString(item, "Ementa") ?? ObterString(item, "ementa");
            if (!string.IsNullOrWhiteSpace(ementa))
            {
                yield return ementa;
            }
        }
    }

    private static BsonArray? ObterArray(BsonDocument documento, string campo)
    {
        return documento.TryGetValue(campo, out var value) && value.IsBsonArray
            ? value.AsBsonArray
            : null;
    }

    private static string? ObterString(BsonDocument documento, string campo)
    {
        return documento.TryGetValue(campo, out var value) && value.IsString
            ? value.AsString
            : null;
    }
}
