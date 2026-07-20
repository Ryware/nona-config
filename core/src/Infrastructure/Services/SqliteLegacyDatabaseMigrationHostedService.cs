using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nona.Libsql;

namespace Nona.Infrastructure.Services;

public sealed class SqliteLegacyDatabaseMigrationHostedService(
    IOptions<SqliteOptions> sqliteOptions,
    ILogger<SqliteLegacyDatabaseMigrationHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var configuredTargetPath = sqliteOptions.Value.DataSource;
        if (configuredTargetPath.Equals(":memory:", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var targetPath = ResolvePath(configuredTargetPath);
        if (File.Exists(targetPath))
        {
            return Task.CompletedTask;
        }

        var legacyDatabaseDirectory = ResolvePath(
            sqliteOptions.Value.LegacySqldDatabasePath);
        var sourcePath = Path.Combine(legacyDatabaseDirectory, "dbs", "default", "data");
        if (!File.Exists(sourcePath))
        {
            return Task.CompletedTask;
        }

        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(targetDirectory);

        var temporaryPath = Path.Combine(
            targetDirectory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.migrating");

        logger.LogInformation(
            "Migrating legacy standalone sqld database from '{SourcePath}' to SQLite database '{TargetPath}'.",
            sourcePath,
            targetPath);

        try
        {
            BackupDatabase(sourcePath, temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
            VerifyIntegrity(temporaryPath);

            try
            {
                File.Move(temporaryPath, targetPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(targetPath))
            {
                File.Delete(temporaryPath);
                logger.LogInformation(
                    "SQLite target '{TargetPath}' was created concurrently; the legacy migration copy was discarded.",
                    targetPath);
                return Task.CompletedTask;
            }

            logger.LogInformation(
                "Legacy standalone database migrated to '{TargetPath}'. The original sqld directory '{LegacyDirectory}' was retained for rollback.",
                targetPath,
                legacyDatabaseDirectory);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void BackupDatabase(string sourcePath, string destinationPath)
    {
        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Default,
            Pooling = false
        }.ToString();
        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = false
        }.ToString();

        using var source = new SqliteConnection(sourceConnectionString);
        using var destination = new SqliteConnection(destinationConnectionString);
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static void VerifyIntegrity(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Default,
            Pooling = false
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        var result = Convert.ToString(command.ExecuteScalar());

        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SQLite integrity check failed for migrated database '{databasePath}': {result ?? "no result"}.");
        }
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
    }
}
