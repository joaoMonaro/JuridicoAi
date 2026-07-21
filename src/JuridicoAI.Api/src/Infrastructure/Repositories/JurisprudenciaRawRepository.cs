using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed record RawSaveResult(JurisprudenciaRaw Raw, bool Criado);

public sealed class JurisprudenciaRawRepository
{
    private readonly IMongoCollection<JurisprudenciaRaw> _collection;

    public JurisprudenciaRawRepository(IOptions<MongoSettings> options)
    {
        var settings = Validar(options.Value);
        var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(Math.Max(1, settings.ServerSelectionTimeoutSeconds));
        clientSettings.ConnectTimeout = clientSettings.ServerSelectionTimeout;
        var database = new MongoClient(clientSettings).GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<JurisprudenciaRaw>(settings.RawCollectionName);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<JurisprudenciaRaw>.IndexKeys
            .Ascending(x => x.Fonte).Ascending(x => x.NumeroProcesso).Ascending(x => x.Link);
        await _collection.Indexes.CreateOneAsync(new CreateIndexModel<JurisprudenciaRaw>(keys,
            new CreateIndexOptions { Unique = true, Name = "uq_fonte_processo_link" }), cancellationToken: cancellationToken);
    }

    public async Task<RawSaveResult> SalvarSeNovoAsync(JurisprudenciaRaw raw, CancellationToken cancellationToken)
    {
        var filtro = Builders<JurisprudenciaRaw>.Filter.Eq(x => x.Fonte, raw.Fonte) &
                     Builders<JurisprudenciaRaw>.Filter.Eq(x => x.NumeroProcesso, raw.NumeroProcesso) &
                     Builders<JurisprudenciaRaw>.Filter.Eq(x => x.Link, raw.Link);
        var existente = await _collection.Find(filtro).FirstOrDefaultAsync(cancellationToken);
        if (existente is not null) return new RawSaveResult(existente, false);

        try
        {
            await _collection.InsertOneAsync(raw, cancellationToken: cancellationToken);
            return new RawSaveResult(raw, true);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existente = await _collection.Find(filtro).FirstAsync(cancellationToken);
            return new RawSaveResult(existente, false);
        }
    }

    public Task<List<JurisprudenciaRaw>> ListarAsync(int limite, CancellationToken cancellationToken) =>
        _collection.Find(Builders<JurisprudenciaRaw>.Filter.Empty)
            .SortByDescending(x => x.DataColeta).Limit(Math.Clamp(limite, 1, 200)).ToListAsync(cancellationToken);

    public Task<List<JurisprudenciaRaw>> ListarParaReprocessarAsync(string? termo, int limite, CancellationToken cancellationToken)
    {
        var filtro = string.IsNullOrWhiteSpace(termo)
            ? Builders<JurisprudenciaRaw>.Filter.Empty
            : Builders<JurisprudenciaRaw>.Filter.Eq(x => x.TermoBuscado, termo.Trim());
        return _collection.Find(filtro).SortByDescending(x => x.DataColeta)
            .Limit(Math.Clamp(limite, 1, 1000)).ToListAsync(cancellationToken);
    }

    public Task AtualizarStatusAsync(string id, string status, string? erro, CancellationToken cancellationToken) =>
        _collection.UpdateOneAsync(x => x.Id == id,
            Builders<JurisprudenciaRaw>.Update.Set(x => x.Status, status).Set(x => x.ErroProcessamento, erro),
            cancellationToken: cancellationToken);

    private static MongoSettings Validar(MongoSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("Configure Mongo:ConnectionString ou a variavel Mongo__ConnectionString.");
        return settings;
    }
}
