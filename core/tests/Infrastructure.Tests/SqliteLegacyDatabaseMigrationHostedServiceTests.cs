using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nona.Infrastructure.Services;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

[NotInParallel]
public class SqliteLegacyDatabaseMigrationHostedServiceTests
{
    [Test]
    public async Task StartAsync_BackupsLegacySqldDatabaseAndRetainsSource()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var legacyDirectory = Path.Combine(root, "primary.db");
            var sourcePath = Path.Combine(legacyDirectory, "dbs", "default", "data");
            var targetPath = Path.Combine(root, "nona.db");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            CreateDatabase(sourcePath, "legacy");

            var service = CreateService(legacyDirectory, targetPath);
            await service.StartAsync(CancellationToken.None);

            await Assert.That(File.Exists(targetPath)).IsTrue();
            await Assert.That(File.Exists(sourcePath)).IsTrue();
            await Assert.That(ReadValue(targetPath)).IsEqualTo("legacy");
            await Assert.That(Directory.GetFiles(root, "*.migrating", SearchOption.TopDirectoryOnly)).IsEmpty();
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Test]
    public async Task StartAsync_DoesNotReplaceExistingSqliteDatabase()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var legacyDirectory = Path.Combine(root, "primary.db");
            var sourcePath = Path.Combine(legacyDirectory, "dbs", "default", "data");
            var targetPath = Path.Combine(root, "nona.db");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            CreateDatabase(sourcePath, "legacy");
            CreateDatabase(targetPath, "current");

            var service = CreateService(legacyDirectory, targetPath);
            await service.StartAsync(CancellationToken.None);

            await Assert.That(ReadValue(targetPath)).IsEqualTo("current");
            await Assert.That(ReadValue(sourcePath)).IsEqualTo("legacy");
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    private static SqliteLegacyDatabaseMigrationHostedService CreateService(
        string legacyDirectory,
        string targetPath)
    {
        return new SqliteLegacyDatabaseMigrationHostedService(
            Options.Create(new SqliteOptions
            {
                DataSource = targetPath,
                TimeoutSeconds = 30,
                LegacySqldDatabasePath = legacyDirectory
            }),
            NullLogger<SqliteLegacyDatabaseMigrationHostedService>.Instance);
    }

    private static void CreateDatabase(string path, string value)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
        }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Sample (Value TEXT NOT NULL); INSERT INTO Sample (Value) VALUES (@value);";
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }

    private static string ReadValue(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Sample";
        return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"nona-legacy-migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
