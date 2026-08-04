using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JuridicoAI.Api.Infrastructure.Database;

public sealed class PostgresConnectionFactory : IDisposable, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresConnectionFactory(IOptions<PostgresSettings> options)
    {
        var connectionString = options.Value.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure Postgres:ConnectionString ou a variavel Postgres__ConnectionString.");
        }

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default) =>
        _dataSource.OpenConnectionAsync(cancellationToken);

    public void Dispose() => _dataSource.Dispose();

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
