using Microsoft.Extensions.Hosting;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Libsql;

namespace Nona.Infrastructure.Services;

public sealed class LibsqlDatabaseInitializer : IHostedService
{
    private readonly ILibsqlDatabaseClient _client;
    private readonly string _migrationsFolder;

    public LibsqlDatabaseInitializer(ILibsqlDatabaseClient client)
    {
        _client = client;

        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _migrationsFolder = Path.Combine(basePath, "Migrations");

        if (!Directory.Exists(_migrationsFolder))
        {
            var currentDir = Directory.GetCurrentDirectory();
            _migrationsFolder = Path.GetFullPath(Path.Combine(currentDir, "..", "Infrastructure", "Migrations"));
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_migrationsFolder))
        {
            Console.WriteLine($"Migrations folder not found at: {_migrationsFolder}");
            Console.WriteLine("Skipping persistent storage migrations.");
            return;
        }

        var directRunner = new LibsqlMigrationRunner(_client, _migrationsFolder);
        await directRunner.RunMigrationsAsync(cancellationToken);
        await NormalizeReleaseEntryKeysAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task NormalizeReleaseEntryKeysAsync(CancellationToken cancellationToken)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, ReleaseVersion, Key
            FROM ConfigReleaseEntries
            WHERE NormalizedKey IS NULL
            """,
            ct: cancellationToken);

        var updates = result.Rows
            .Select(row => new LibsqlStatement(
                """
                UPDATE ConfigReleaseEntries
                SET NormalizedKey = @NormalizedKey
                WHERE Project = @Project COLLATE NOCASE
                  AND Environment = @Environment COLLATE NOCASE
                  AND ReleaseVersion = @ReleaseVersion COLLATE NOCASE
                  AND Key = @Key COLLATE BINARY
                """,
                LibsqlParameters.Create(
                    ("Project", row.GetString("Project")),
                    ("Environment", row.GetString("Environment")),
                    ("ReleaseVersion", row.GetString("ReleaseVersion")),
                    ("Key", row.GetString("Key")),
                    ("NormalizedKey", LibsqlConfigReleaseRepository.NormalizeKey(row.GetString("Key"))))))
            .ToList();

        foreach (var batch in updates.Chunk(500))
        {
            await _client.ExecuteBatchAsync(batch, cancellationToken);
        }
    }
}
