namespace Nona.Libsql;

public sealed class LibsqlMigrationRunner
{
    private const string CreateMigrationsTableSql = """
        CREATE TABLE IF NOT EXISTS __MigrationsHistory (
            MigrationId TEXT PRIMARY KEY,
            AppliedAt TEXT NOT NULL
        )
        """;

    private readonly ILibsqlDatabaseClient _client;
    private readonly string _migrationsFolder;

    public LibsqlMigrationRunner(ILibsqlDatabaseClient client, string migrationsFolder)
    {
        _client = client;
        _migrationsFolder = migrationsFolder;
    }

    public async Task RunMigrationsAsync(CancellationToken ct = default)
    {
        await _client.ExecuteAsync(CreateMigrationsTableSql, ct: ct);

        var migrationFiles = Directory.GetFiles(_migrationsFolder, "*.sql")
            .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var migrationFile in migrationFiles)
        {
            var migrationName = Path.GetFileName(migrationFile);

            if (await IsMigrationAppliedAsync(migrationName, ct))
            {
                continue;
            }

            var script = await File.ReadAllTextAsync(migrationFile, ct);
            var statements = SqlScriptParser.SplitStatements(script)
                .Select(sql => new LibsqlStatement(sql))
                .ToList();

            statements.Add(new LibsqlStatement(
                "INSERT OR IGNORE INTO __MigrationsHistory (MigrationId, AppliedAt) VALUES (@MigrationId, @AppliedAt)",
                new { MigrationId = migrationName, AppliedAt = DateTime.UtcNow.ToString("O") }));

            await _client.ExecuteBatchAsync(statements, ct);
        }
    }

    private async Task<bool> IsMigrationAppliedAsync(string migrationName, CancellationToken ct)
    {
        var result = await _client.ExecuteAsync(
            "SELECT COUNT(1) FROM __MigrationsHistory WHERE MigrationId = @MigrationId",
            new { MigrationId = migrationName },
            ct);

        return result.Rows.Count > 0 && result.Rows[0].GetInt32(0) > 0;
    }
}
