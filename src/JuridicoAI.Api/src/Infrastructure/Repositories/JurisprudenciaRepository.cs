using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class JurisprudenciaRepository
{
    private readonly IMongoCollection<Jurisprudencia> _collection;

    public JurisprudenciaRepository(IOptions<MongoSettings> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("Configure Mongo:ConnectionString ou a variavel Mongo__ConnectionString.");
        var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(Math.Max(1, settings.ServerSelectionTimeoutSeconds));
        clientSettings.ConnectTimeout = clientSettings.ServerSelectionTimeout;
        var database = new MongoClient(clientSettings).GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<Jurisprudencia>(settings.JurisprudenciasCollectionName);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var models = new[]
        {
            new CreateIndexModel<Jurisprudencia>(Builders<Jurisprudencia>.IndexKeys.Ascending(x => x.RawId),
                new CreateIndexOptions { Unique = true, Name = "uq_raw_id" }),
            new CreateIndexModel<Jurisprudencia>(Builders<Jurisprudencia>.IndexKeys.Ascending(x => x.Fonte)),
            new CreateIndexModel<Jurisprudencia>(Builders<Jurisprudencia>.IndexKeys.Ascending(x => x.NumeroProcesso)),
            new CreateIndexModel<Jurisprudencia>(Builders<Jurisprudencia>.IndexKeys.Ascending(x => x.Link)),
            new CreateIndexModel<Jurisprudencia>(Builders<Jurisprudencia>.IndexKeys.Ascending("classificacao.tema")),
            new CreateIndexModel<Jurisprudencia>(Builders<Jurisprudencia>.IndexKeys.Ascending("classificacao.subtema")),
            new CreateIndexModel<Jurisprudencia>(Builders<Jurisprudencia>.IndexKeys.Ascending("classificacao.resultado")),
            new CreateIndexModel<Jurisprudencia>(Builders<Jurisprudencia>.IndexKeys.Ascending("classificacao.danoMoral")),
            new CreateIndexModel<Jurisprudencia>(Builders<Jurisprudencia>.IndexKeys.Descending(x => x.DataJulgamento))
        };
        await _collection.Indexes.CreateManyAsync(models, cancellationToken);
    }

    public async Task SalvarAsync(Jurisprudencia jurisprudencia, CancellationToken cancellationToken)
    {
        var existente = await _collection.Find(x => x.RawId == jurisprudencia.RawId)
            .FirstOrDefaultAsync(cancellationToken);

        // O driver nao gera ObjectId automaticamente em ReplaceOne com upsert.
        // Em reprocessamentos, o _id original precisa ser preservado por ser imutavel.
        jurisprudencia.Id = existente is null
            ? ObjectId.GenerateNewId().ToString()
            : existente.Id;

        await _collection.ReplaceOneAsync(x => x.RawId == jurisprudencia.RawId, jurisprudencia,
            new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public Task<List<Jurisprudencia>> ConsultarAsync(JurisprudenciaQuery query, CancellationToken cancellationToken)
    {
        var filtros = new List<FilterDefinition<Jurisprudencia>>();
        if (!string.IsNullOrWhiteSpace(query.Tema)) filtros.Add(Builders<Jurisprudencia>.Filter.Eq("classificacao.tema", query.Tema));
        if (!string.IsNullOrWhiteSpace(query.Subtema)) filtros.Add(Builders<Jurisprudencia>.Filter.Eq("classificacao.subtema", query.Subtema));
        if (!string.IsNullOrWhiteSpace(query.Resultado)) filtros.Add(Builders<Jurisprudencia>.Filter.Eq("classificacao.resultado", query.Resultado));
        if (query.DanoMoral.HasValue) filtros.Add(Builders<Jurisprudencia>.Filter.Eq("classificacao.danoMoral", query.DanoMoral.Value));
        if (!string.IsNullOrWhiteSpace(query.Fonte)) filtros.Add(Builders<Jurisprudencia>.Filter.Eq(x => x.Fonte, query.Fonte.ToUpperInvariant()));
        var filtro = filtros.Count == 0 ? Builders<Jurisprudencia>.Filter.Empty : Builders<Jurisprudencia>.Filter.And(filtros);
        return _collection.Find(filtro).SortByDescending(x => x.DataJulgamento)
            .Limit(Math.Clamp(query.Limite, 1, 200)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ObterEmentasAsync(CancellationToken cancellationToken = default) =>
        await _collection.Find(Builders<Jurisprudencia>.Filter.Empty)
            .SortByDescending(x => x.Classificacao.Confianca).Limit(30)
            .Project(x => x.Ementa).ToListAsync(cancellationToken);
}
