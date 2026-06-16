using JuridicoAI.Application.Services;
using JuridicoAI.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JuridicoAI.Infrastructure.Repositories;

public sealed class MongoJurisprudenciaRepository : IJurisprudenciaProvider
{
    private const string TribunalNome = "TJSP";

    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly ILogger<MongoJurisprudenciaRepository> _logger;

    public MongoJurisprudenciaRepository(
        IConfiguration configuration,
        ILogger<MongoJurisprudenciaRepository> logger)
    {
        _logger = logger;

        var connectionString = configuration["Mongo:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configure Mongo:ConnectionString para consultar jurisprudencias.");
        }

        var databaseName = configuration["Mongo:Database"] ?? "JuridicoAi";
        var collectionName = configuration["Mongo:Collection"] ?? "tjsp_search_results";

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<BsonDocument>(collectionName);
    }

    public string Tribunal => TribunalNome;

    public async Task<IReadOnlyList<JurisprudenciaResultado>> PesquisarAsync(
        string termo,
        int quantidadeMaxima = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            throw new ArgumentException("O termo de pesquisa deve ser informado.", nameof(termo));
        }

        if (quantidadeMaxima <= 0)
        {
            return [];
        }

        var termoRegex = new BsonRegularExpression(termo, "i");
        var filter = Builders<BsonDocument>.Filter.Regex("termo", termoRegex);
        var sort = Builders<BsonDocument>.Sort.Descending("createdAtUtc");

        var documentos = await _collection
            .Find(filter)
            .Sort(sort)
            .Limit(5)
            .ToListAsync(cancellationToken);

        var resultados = documentos
            .SelectMany(ExtrairResultados)
            .Where(resultado =>
                !string.IsNullOrWhiteSpace(resultado.NumeroProcesso) ||
                !string.IsNullOrWhiteSpace(resultado.Ementa))
            .GroupBy(resultado => $"{resultado.Tribunal}|{resultado.NumeroProcesso}|{resultado.Link}")
            .Select(group => group.First())
            .Take(quantidadeMaxima)
            .ToList();

        _logger.LogInformation(
            "Consulta de jurisprudencias no Mongo finalizada. Termo: {Termo}. Resultados: {Quantidade}",
            termo,
            resultados.Count);

        return resultados;
    }

    private static IEnumerable<JurisprudenciaResultado> ExtrairResultados(BsonDocument document)
    {
        if (!document.TryGetValue("result", out var resultValue) || !resultValue.IsBsonDocument)
        {
            yield break;
        }

        var result = resultValue.AsBsonDocument;
        if (!result.TryGetValue("Resultados", out var resultadosValue) || !resultadosValue.IsBsonArray)
        {
            yield break;
        }

        foreach (var itemValue in resultadosValue.AsBsonArray)
        {
            if (!itemValue.IsBsonDocument)
            {
                continue;
            }

            var item = itemValue.AsBsonDocument;
            yield return new JurisprudenciaResultado
            {
                Tribunal = ObterString(item, "Tribunal") ?? TribunalNome,
                NumeroProcesso = ObterString(item, "NumeroProcesso") ?? string.Empty,
                Ementa = ObterString(item, "Ementa") ?? string.Empty,
                Link = ObterString(item, "Link") ??
                       ObterString(item, "LinkPdf") ??
                       string.Empty,
                Relator = ObterString(item, "Relator"),
                DataJulgamento = ObterString(item, "DataJulgamento")
            };
        }
    }

    private static string? ObterString(BsonDocument document, string fieldName)
    {
        return document.TryGetValue(fieldName, out var value) && !value.IsBsonNull
            ? value.ToString()
            : null;
    }
}
