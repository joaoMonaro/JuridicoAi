using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JuridicoAI.Api.Infrastructure.Database;

public sealed class DatabaseMigrationService(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    private const string InitialMigration = "20260812002119_InitialCreate";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Postgres:RunMigrations", true)) return;
        if (string.IsNullOrWhiteSpace(configuration["Postgres:ConnectionString"]))
        {
            logger.LogWarning("PostgreSQL nao configurado; migrations do EF Core nao foram executadas.");
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<JuridicoAiDbContext>();
        await ImportLegacyHistoryAsync(context, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ImportLegacyHistoryAsync(
        JuridicoAiDbContext context, CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT to_regclass('public.casos') IS NOT NULL
                   AND to_regclass('public.schema_migrations') IS NOT NULL
                """;
            if (await command.ExecuteScalarAsync(cancellationToken) is not true) return;

            command.CommandText =
                "SELECT EXISTS (SELECT 1 FROM schema_migrations WHERE version = '001_fundacao_caso.sql')";
            if (await command.ExecuteScalarAsync(cancellationToken) is not true) return;

            var historyRepository = context.GetService<IHistoryRepository>();
            await context.Database.ExecuteSqlRawAsync(historyRepository.GetCreateIfNotExistsScript(), cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ({InitialMigration}, {ProductInfo.GetVersion()})
                ON CONFLICT ("MigrationId") DO NOTHING
                """, cancellationToken);
            logger.LogInformation("Historico da migration SQL legada importado para o EF Core.");
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
