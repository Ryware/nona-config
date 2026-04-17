using Nona.Libsql;

namespace Nona.StorageBenchmarks;

internal static class DatabaseSeeder
{
    public const string ProjectName = "bench-project";
    public const string ProjectSlug = "bench-project";
    public const string ServerApiKey = "BENCH-SERVER-KEY";
    public const string ClientApiKey = "BENCH-CLIENT-KEY";

    public static readonly IReadOnlyDictionary<DatasetSize, int> DatasetRows = new Dictionary<DatasetSize, int>
    {
        [DatasetSize.Small] = 1,
        [DatasetSize.Medium] = 10_000,
        [DatasetSize.Large] = 1_000_000
    };

    public static string GetEnvironmentName(DatasetSize dataset)
    {
        return dataset switch
        {
            DatasetSize.Small => "small",
            DatasetSize.Medium => "medium",
            DatasetSize.Large => "large",
            _ => throw new ArgumentOutOfRangeException(nameof(dataset), dataset, null)
        };
    }

    public static async Task CreateSeedDatabaseAsync(
        string databasePath,
        string migrationsDirectory,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        using var client = SqlStatementFactory.CreateLocalClient(databasePath);
        await SeedLibsqlDatabaseAsync(client, migrationsDirectory, cancellationToken);
    }

    public static void CopySeedDatabase(string sourcePath, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Copy(sourcePath, destinationPath);
    }

    public static async Task SeedLibsqlDatabaseAsync(
        ILibsqlDatabaseClient client,
        string migrationsDirectory,
        CancellationToken cancellationToken)
    {
        var migrationRunner = new LibsqlMigrationRunner(client, migrationsDirectory);
        await migrationRunner.RunMigrationsAsync(cancellationToken);
        await ClearExistingBenchmarkDataAsync(client, cancellationToken);
        await SeedCoreMetadataAsync(client, cancellationToken);

        foreach (var pair in DatasetRows)
        {
            Console.WriteLine($"Seeding libsql {pair.Key} dataset with {pair.Value:N0} rows.");
            await SeedConfigEntriesAsync(client, pair.Key, pair.Value, cancellationToken);
        }
    }

    public static string BuildKey(int index)
    {
        return $"KEY_{index:D7}";
    }

    private static async Task SeedCoreMetadataAsync(ILibsqlDatabaseClient client, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.ToString("O");

        await client.ExecuteAsync(
            """
            INSERT INTO Projects (Name, UrlSlug, ServerApiKey, ClientApiKey, CreatedAt, UpdatedAt)
            VALUES (@Name, @Slug, @ServerApiKey, @ClientApiKey, @CreatedAt, @UpdatedAt)
            """,
            new
            {
                Name = ProjectName,
                Slug = ProjectSlug,
                ServerApiKey,
                ClientApiKey,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        foreach (var dataset in DatasetRows.Keys)
        {
            await client.ExecuteAsync(
                """
                INSERT INTO Environments (Name, Project, CreatedAt, UpdatedAt)
                VALUES (@Name, @Project, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    Name = GetEnvironmentName(dataset),
                    Project = ProjectName,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                cancellationToken);
        }
    }

    private static async Task SeedConfigEntriesAsync(
        ILibsqlDatabaseClient client,
        DatasetSize dataset,
        int rowCount,
        CancellationToken cancellationToken)
    {
        const int batchSize = 1_000;

        var environment = GetEnvironmentName(dataset);
        var now = DateTime.UtcNow.ToString("O");
        var batch = new List<LibsqlStatement>(batchSize);

        for (var index = 1; index <= rowCount; index++)
        {
            batch.Add(new LibsqlStatement(
                """
                INSERT INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt)
                VALUES (@Project, @Environment, @Key, @Value, @ContentType, @Scope, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    Project = ProjectName,
                    Environment = environment,
                    Key = BuildKey(index),
                    Value = BuildValue(dataset, index),
                    ContentType = "string",
                    Scope = 3,
                    CreatedAt = now,
                    UpdatedAt = now
                }));

            if (batch.Count == batchSize || index == rowCount)
            {
                await client.ExecuteBatchAsync(batch, cancellationToken);
                batch.Clear();
            }

            if (index % 50_000 == 0 || index == rowCount)
            {
                Console.WriteLine($"  {dataset}: {index:N0}/{rowCount:N0}");
            }
        }
    }

    private static async Task ClearExistingBenchmarkDataAsync(ILibsqlDatabaseClient client, CancellationToken cancellationToken)
    {
        await client.ExecuteBatchAsync(
        [
            new LibsqlStatement(
                "DELETE FROM ConfigEntries WHERE Project = @Project",
                new { Project = ProjectName }),
            new LibsqlStatement(
                "DELETE FROM Environments WHERE Project = @Project",
                new { Project = ProjectName }),
            new LibsqlStatement(
                "DELETE FROM Projects WHERE Name = @Name OR UrlSlug = @Slug",
                new { Name = ProjectName, Slug = ProjectSlug })
        ], cancellationToken);
    }

    private static string BuildValue(DatasetSize dataset, int index)
    {
        return $"{dataset.ToString().ToLowerInvariant()}-value-{index:D7}-abcdefghijklmnopqrstuvwxyz0123456789";
    }
}
