using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Nona.Benchmarks;

namespace Nona.Benchmarks.Tests;

public class BenchmarkApiKeyTests
{
    [Test]
    public async Task ApiKeySeed_IsReadableByBenchmarkWorkloads()
    {
        var root = ResolveRepoRoot();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"nona-benchmark-api-key-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(temporaryDirectory, "benchmark.db");

        try
        {
            await DatabaseSeeder.CreateSeedDatabaseAsync(
                databasePath,
                Path.Combine(root, "core", "src", "Infrastructure", "Migrations"),
                CancellationToken.None);

            await AssertStoredApiKeyAsync(databasePath);

            var scenario = new BenchmarkScenario(
                "api-key-smoke",
                DatasetSize.Small,
                WorkloadKind.PointLookup,
                1,
                1,
                true);

            await using (var sqlite = new SqliteBenchmarkDatabase("sqlite", databasePath))
            {
                await sqlite.InitializeAsync(CancellationToken.None);
                await sqlite.ExecuteAsync(scenario, new Random(1), CancellationToken.None);
            }

            await using (var sqliteClient = new DatabaseClientBenchmarkDatabase(
                             "sqlite-client",
                             SqlStatementFactory.CreateLocalClient(databasePath)))
            {
                await sqliteClient.InitializeAsync(CancellationToken.None);
                await sqliteClient.ExecuteAsync(scenario, new Random(1), CancellationToken.None);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    private static async Task AssertStoredApiKeyAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = "PRAGMA table_info(ApiKeys)";
        var columns = new List<string>();
        await using (var reader = await columnsCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }

        await Assert.That(columns).Contains("KeyHash");
        await Assert.That(columns).DoesNotContain("Key");

        await using var keyCommand = connection.CreateCommand();
        keyCommand.CommandText =
            "SELECT KeyHash, Fingerprint, HashVersion FROM ApiKeys WHERE Project = $project";
        keyCommand.Parameters.AddWithValue("$project", DatabaseSeeder.ProjectName);
        await using var keyReader = await keyCommand.ExecuteReaderAsync();
        await Assert.That(await keyReader.ReadAsync()).IsTrue();

        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(DatabaseSeeder.ApiKey)));
        await Assert.That(keyReader.GetString(0)).IsEqualTo(expectedHash);
        await Assert.That(keyReader.GetString(1)).IsEqualTo(DatabaseSeeder.ApiKey[^8..]);
        await Assert.That(keyReader.GetInt32(2)).IsEqualTo(1);
        var isCanonicalSecret = DatabaseSeeder.ApiKey.Length == 64
                                && DatabaseSeeder.ApiKey.All(character =>
                                    character is >= '0' and <= '9' or >= 'A' and <= 'F');
        await Assert.That(isCanonicalSecret).IsTrue();
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NonaConfig.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
