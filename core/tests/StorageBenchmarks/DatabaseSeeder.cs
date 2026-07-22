using Nona.Libsql;

namespace Nona.StorageBenchmarks;

internal static class DatabaseSeeder
{
    public const string ProjectName = "bench-project";
    public const string ProjectSlug = "bench-project";
    public const string ApiKey = "BENCH-SCOPED-KEY";
    public const string ReleaseVersion = "1.0.0";

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
            INSERT INTO Projects (Name, UrlSlug, CreatedAt, UpdatedAt)
            VALUES (@Name, @Slug, @CreatedAt, @UpdatedAt)
            """,
            LibsqlParameters.Create(
                ("Name", ProjectName),
                ("Slug", ProjectSlug),
                ("CreatedAt", now),
                ("UpdatedAt", now)),
            cancellationToken);

        await client.ExecuteAsync(
            """
            INSERT INTO ApiKeys (Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt)
            VALUES (@Name, @Key, @Project, NULL, @Scope, @CreatedAt, @UpdatedAt)
            """,
            LibsqlParameters.Create(
                ("Name", "Benchmark"),
                ("Key", ApiKey),
                ("Project", ProjectName),
                ("Scope", 3),
                ("CreatedAt", now),
                ("UpdatedAt", now)),
            cancellationToken);

        foreach (var dataset in DatasetRows.Keys)
        {
            await client.ExecuteAsync(
                """
                INSERT INTO Environments (Name, Project, ActiveReleaseVersion, CreatedAt, UpdatedAt)
                VALUES (@Name, @Project, @ReleaseVersion, @CreatedAt, @UpdatedAt)
                """,
                LibsqlParameters.Create(
                    ("Name", GetEnvironmentName(dataset)),
                    ("Project", ProjectName),
                    ("ReleaseVersion", ReleaseVersion),
                    ("CreatedAt", now),
                    ("UpdatedAt", now)),
                cancellationToken);

            await client.ExecuteAsync(
                """
                INSERT INTO ConfigReleases (Project, Environment, Version, Major, Minor, Patch, CreatedAt, Actor)
                VALUES (@Project, @Environment, @Version, 1, 0, 0, @CreatedAt, 'Benchmark')
                """,
                LibsqlParameters.Create(
                    ("Project", ProjectName),
                    ("Environment", GetEnvironmentName(dataset)),
                    ("Version", ReleaseVersion),
                    ("CreatedAt", now)),
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
                LibsqlParameters.Create(
                    ("Project", ProjectName),
                    ("Environment", environment),
                    ("Key", BuildKey(index)),
                    ("Value", BuildValue(dataset, index)),
                    ("ContentType", "text"),
                    ("Scope", 3),
                    ("CreatedAt", now),
                    ("UpdatedAt", now))));

            batch.Add(new LibsqlStatement(
                """
                INSERT INTO ConfigReleaseEntries (Project, Environment, ReleaseVersion, Key, Value, ContentType, Scope)
                VALUES (@Project, @Environment, @ReleaseVersion, @Key, @Value, @ContentType, @Scope)
                """,
                LibsqlParameters.Create(
                    ("Project", ProjectName),
                    ("Environment", environment),
                    ("ReleaseVersion", ReleaseVersion),
                    ("Key", BuildKey(index)),
                    ("Value", BuildValue(dataset, index)),
                    ("ContentType", "text"),
                    ("Scope", 3))));

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
                "DELETE FROM ApiKeys WHERE Project = @Project",
                LibsqlParameters.Create(("Project", ProjectName))),
            new LibsqlStatement(
                "DELETE FROM ConfigReleaseEntries WHERE Project = @Project",
                LibsqlParameters.Create(("Project", ProjectName))),
            new LibsqlStatement(
                "DELETE FROM ConfigReleases WHERE Project = @Project",
                LibsqlParameters.Create(("Project", ProjectName))),
            new LibsqlStatement(
                "DELETE FROM ConfigEntries WHERE Project = @Project",
                LibsqlParameters.Create(("Project", ProjectName))),
            new LibsqlStatement(
                "DELETE FROM Environments WHERE Project = @Project",
                LibsqlParameters.Create(("Project", ProjectName))),
            new LibsqlStatement(
                "DELETE FROM Projects WHERE Name = @Name OR UrlSlug = @Slug",
                LibsqlParameters.Create(("Name", ProjectName), ("Slug", ProjectSlug)))
        ], cancellationToken);
    }

    private static string BuildValue(DatasetSize dataset, int index)
    {
        return $"{dataset.ToString().ToLowerInvariant()}-value-{index:D7}-abcdefghijklmnopqrstuvwxyz0123456789";
    }
}
