using Microsoft.Data.Sqlite;
using Nona.Libsql;
using System.Security.Cryptography;
using System.Text;

namespace Nona.Benchmarks;

public static class DatabaseSeeder
{
    public const string ProjectName = "bench-project";
    public const string ProjectSlug = "bench-project";
    public const string ApiKey = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    public const string ReleaseVersion = "1.0.0";
    internal static string ApiKeyHash { get; } = HashApiKey(ApiKey);

    public static readonly IReadOnlyDictionary<DatasetSize, int> DatasetRows = new Dictionary<DatasetSize, int>
    {
        [DatasetSize.Small] = 1,
        [DatasetSize.Medium] = 100,
        [DatasetSize.Large] = 10_000
    };

    public static readonly IReadOnlyDictionary<DatasetSize, int> DatasetValueBytes = new Dictionary<DatasetSize, int>
    {
        [DatasetSize.Small] = 64,
        [DatasetSize.Medium] = 1_024,
        [DatasetSize.Large] = 16_384
    };

    public static string GetEnvironmentName(DatasetSize dataset)
    {
        return dataset switch
        {
            DatasetSize.Small => "keys-1",
            DatasetSize.Medium => "keys-100",
            DatasetSize.Large => "keys-10000",
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

        using (var client = new SqliteDatabaseClient(databasePath, commandTimeoutSeconds: 60))
        {
            await SeedLibsqlDatabaseAsync(client, migrationsDirectory, cancellationToken);
            await client.ExecuteAsync("PRAGMA wal_checkpoint(TRUNCATE)", ct: cancellationToken);
        }

        SqliteConnection.ClearAllPools();
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
            Console.WriteLine($"Seeding {pair.Key} dataset with {pair.Value:N0} rows.");
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

        await client.ExecuteBatchAsync(
            [CreateApiKeyInsertStatement("Benchmark", ApiKey, ProjectName, null, 3, now)],
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
                     ("Value", BuildValue(dataset, environment, index)),
                    ("ContentType", "text"),
                    ("Scope", 3),
                    ("CreatedAt", now),
                    ("UpdatedAt", now))));

            batch.Add(new LibsqlStatement(
                """
                INSERT INTO ConfigReleaseEntries (
                    Project, Environment, ReleaseVersion, Key, NormalizedKey, Value, ContentType, Scope
                )
                VALUES (
                    @Project, @Environment, @ReleaseVersion, @Key, @NormalizedKey, @Value, @ContentType, @Scope
                )
                """,
                LibsqlParameters.Create(
                    ("Project", ProjectName),
                    ("Environment", environment),
                    ("ReleaseVersion", ReleaseVersion),
                    ("Key", BuildKey(index)),
                    ("NormalizedKey", BuildKey(index).ToUpperInvariant()),
                     ("Value", BuildValue(dataset, environment, index)),
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

    public static string BuildValue(DatasetSize dataset, string environment, int index)
    {
        var prefix = $"{environment}-value-{index:D7}-";
        var value = new string('x', DatasetValueBytes[dataset]);
        return prefix.Length >= value.Length ? prefix[..value.Length] : prefix + value[prefix.Length..];
    }

    public static string BuildValue(string environment, int index)
    {
        var dataset = DatasetRows.Keys.Single(dataset =>
            string.Equals(GetEnvironmentName(dataset), environment, StringComparison.OrdinalIgnoreCase));
        return BuildValue(dataset, environment, index);
    }

    internal static LibsqlStatement CreateApiKeyInsertStatement(
        string name,
        string secret,
        string project,
        string? environment,
        int scope,
        string timestamp)
    {
        return new LibsqlStatement(
            """
            INSERT INTO ApiKeys (
                Name, KeyHash, Fingerprint, HashVersion, Project, Environment, Scope, CreatedAt, UpdatedAt
            )
            VALUES (
                @Name, @KeyHash, @Fingerprint, 1, @Project, @Environment, @Scope, @CreatedAt, @UpdatedAt
            )
            """,
            LibsqlParameters.Create(
                ("Name", name),
                ("KeyHash", HashApiKey(secret)),
                ("Fingerprint", secret[^8..]),
                ("Project", project),
                ("Environment", environment),
                ("Scope", scope),
                ("CreatedAt", timestamp),
                ("UpdatedAt", timestamp)));
    }

    private static string HashApiKey(string secret)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}

