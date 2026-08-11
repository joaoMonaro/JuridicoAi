using System.Reflection;
using Npgsql;

namespace JuridicoAI.Api.Infrastructure.Database;

public sealed class DatabaseMigrationService(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Postgres:RunMigrations", true)) return;
        if (string.IsNullOrWhiteSpace(configuration["Postgres:ConnectionString"]))
        {
            logger.LogWarning("PostgreSQL nao configurado; migrations da Fundacao do Caso nao foram executadas.");
            return;
        }

        var factory = services.GetRequiredService<PostgresConnectionFactory>();
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await EnsureHistoryTableAsync(connection, cancellationToken);

        var assembly = typeof(DatabaseMigrationService).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Database.Migrations.", StringComparison.Ordinal)
                           && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        foreach (var resource in resources)
        {
            var version = resource[(resource.LastIndexOf(".Migrations.", StringComparison.Ordinal) + 12)..];
            if (await IsAppliedAsync(connection, version, cancellationToken)) continue;
            await ApplyAsync(connection, assembly, resource, version, cancellationToken);
            logger.LogInformation("Migration PostgreSQL {Migration} aplicada.", version);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureHistoryTableAsync(
        NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version varchar(255) PRIMARY KEY,
                applied_at timestamptz NOT NULL
            )
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsAppliedAsync(
        NpgsqlConnection connection, string version, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM schema_migrations WHERE version = @version)";
        command.Parameters.AddWithValue("version", version);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task ApplyAsync(
        NpgsqlConnection connection,
        Assembly assembly,
        string resource,
        string version,
        CancellationToken cancellationToken)
    {
        await using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Migration embutida nao encontrada: {resource}");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var migration = connection.CreateCommand())
            {
                migration.Transaction = transaction;
                migration.CommandText = sql;
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var history = connection.CreateCommand())
            {
                history.Transaction = transaction;
                history.CommandText = "INSERT INTO schema_migrations (version, applied_at) VALUES (@version, @applied_at)";
                history.Parameters.AddWithValue("version", version);
                history.Parameters.AddWithValue("applied_at", DateTime.UtcNow);
                await history.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
