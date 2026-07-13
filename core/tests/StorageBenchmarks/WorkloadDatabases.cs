using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Nona.Libsql;

namespace Nona.StorageBenchmarks;

internal interface IBenchmarkDatabase : IAsyncDisposable
{
    string ProviderName { get; }
    Task InitializeAsync(CancellationToken cancellationToken);
    Task ExecuteAsync(BenchmarkScenario scenario, Random random, CancellationToken cancellationToken);
}

internal sealed class LibsqlBenchmarkDatabase : IBenchmarkDatabase
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlBenchmarkDatabase(string providerName, ILibsqlDatabaseClient client)
    {
        ProviderName = providerName;
        _client = client;
    }

    public string ProviderName { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _client.ExecuteAsync("SELECT 1", ct: cancellationToken);
    }

    public async Task ExecuteAsync(BenchmarkScenario scenario, Random random, CancellationToken cancellationToken)
    {
        var datasetRows = DatabaseSeeder.DatasetRows[scenario.Dataset];
        var environment = DatabaseSeeder.GetEnvironmentName(scenario.Dataset);

        await EnsureProjectExistsAsync(cancellationToken);
        await EnsureEnvironmentExistsAsync(environment, cancellationToken);

        var rowCount = scenario.Workload switch
        {
            WorkloadKind.PointLookup => await ExecutePointLookupAsync(environment, scenario.ItemCount, datasetRows, random, cancellationToken),
            WorkloadKind.RangeQuery => await ExecuteRangeQueryAsync(environment, scenario.ItemCount, datasetRows, random, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario.Workload), scenario.Workload, null)
        };

        if (rowCount != scenario.ItemCount)
        {
            throw new InvalidOperationException(
                $"Expected {scenario.ItemCount} rows for {scenario.Name}, received {rowCount}.");
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private async Task EnsureProjectExistsAsync(CancellationToken cancellationToken)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT p.Name
            FROM ApiKeys ak
            INNER JOIN Projects p ON p.Name = ak.Project COLLATE NOCASE
            WHERE ak.Key = @ApiKey
            LIMIT 1
            """,
            new Dictionary<string, object?>
            {
                ["ApiKey"] = DatabaseSeeder.ApiKey
            },
            cancellationToken);

        if (result.Rows.Count == 0)
        {
            throw new InvalidOperationException("Benchmark project lookup returned no rows.");
        }
    }

    private async Task EnsureEnvironmentExistsAsync(string environment, CancellationToken cancellationToken)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM Environments
            WHERE Project = @Project COLLATE NOCASE
              AND Name = @Environment COLLATE NOCASE
            """,
            new Dictionary<string, object?>
            {
                ["Project"] = DatabaseSeeder.ProjectName,
                ["Environment"] = environment
            },
            cancellationToken);

        if (result.Rows.Count == 0 || result.Rows[0].GetInt32(0) <= 0)
        {
            throw new InvalidOperationException($"Benchmark environment '{environment}' was not found.");
        }
    }

    private async Task<int> ExecutePointLookupAsync(
        string environment,
        int keyCount,
        int datasetRows,
        Random random,
        CancellationToken cancellationToken)
    {
        var startIndex = random.Next(1, datasetRows - keyCount + 2);
        var (sql, parameters) = SqlStatementFactory.BuildPointLookup(environment, startIndex, keyCount);
        var result = await _client.ExecuteAsync(sql, parameters, cancellationToken);
        return result.Rows.Count;
    }

    private async Task<int> ExecuteRangeQueryAsync(
        string environment,
        int limit,
        int datasetRows,
        Random random,
        CancellationToken cancellationToken)
    {
        var maxOffset = Math.Max(0, datasetRows - limit);
        var offset = maxOffset == 0 ? 0 : random.Next(0, maxOffset + 1);
        var (sql, parameters) = SqlStatementFactory.BuildRangeQuery(environment, limit, offset);
        var result = await _client.ExecuteAsync(sql, parameters, cancellationToken);
        return result.Rows.Count;
    }
}

internal sealed class SqliteBenchmarkDatabase : IBenchmarkDatabase
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;
    private bool _disposed;

    public SqliteBenchmarkDatabase(string providerName, string databasePath)
    {
        ProviderName = providerName;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public string ProviderName { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await ExecuteScalarAsync("SELECT 1", null, cancellationToken);
    }

    public async Task ExecuteAsync(BenchmarkScenario scenario, Random random, CancellationToken cancellationToken)
    {
        var datasetRows = DatabaseSeeder.DatasetRows[scenario.Dataset];
        var environment = DatabaseSeeder.GetEnvironmentName(scenario.Dataset);

        await EnsureProjectExistsAsync(cancellationToken);
        await EnsureEnvironmentExistsAsync(environment, cancellationToken);

        var rowCount = scenario.Workload switch
        {
            WorkloadKind.PointLookup => await ExecutePointLookupAsync(environment, scenario.ItemCount, datasetRows, random, cancellationToken),
            WorkloadKind.RangeQuery => await ExecuteRangeQueryAsync(environment, scenario.ItemCount, datasetRows, random, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario.Workload), scenario.Workload, null)
        };

        if (rowCount != scenario.ItemCount)
        {
            throw new InvalidOperationException(
                $"Expected {scenario.ItemCount} rows for {scenario.Name}, received {rowCount}.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }

    private async Task EnsureProjectExistsAsync(CancellationToken cancellationToken)
    {
        var rowCount = await ExecuteReaderCountAsync(
            """
            SELECT p.Name
            FROM ApiKeys ak
            INNER JOIN Projects p ON p.Name = ak.Project COLLATE NOCASE
            WHERE ak.Key = @ApiKey
            LIMIT 1
            """,
            new Dictionary<string, object?>
            {
                ["ApiKey"] = DatabaseSeeder.ApiKey
            },
            cancellationToken);

        if (rowCount == 0)
        {
            throw new InvalidOperationException("Benchmark project lookup returned no rows.");
        }
    }

    private async Task EnsureEnvironmentExistsAsync(string environment, CancellationToken cancellationToken)
    {
        var count = await ExecuteScalarAsync(
            """
            SELECT COUNT(1)
            FROM Environments
            WHERE Project = @Project COLLATE NOCASE
              AND Name = @Environment COLLATE NOCASE
            """,
            new Dictionary<string, object?>
            {
                ["Project"] = DatabaseSeeder.ProjectName,
                ["Environment"] = environment
            },
            cancellationToken);

        if (Convert.ToInt32(count, System.Globalization.CultureInfo.InvariantCulture) <= 0)
        {
            throw new InvalidOperationException($"Benchmark environment '{environment}' was not found.");
        }
    }

    private async Task<int> ExecutePointLookupAsync(
        string environment,
        int keyCount,
        int datasetRows,
        Random random,
        CancellationToken cancellationToken)
    {
        var startIndex = random.Next(1, datasetRows - keyCount + 2);
        var (sql, parameters) = SqlStatementFactory.BuildPointLookup(environment, startIndex, keyCount);
        return await ExecuteReaderCountAsync(sql, parameters, cancellationToken);
    }

    private async Task<int> ExecuteRangeQueryAsync(
        string environment,
        int limit,
        int datasetRows,
        Random random,
        CancellationToken cancellationToken)
    {
        var maxOffset = Math.Max(0, datasetRows - limit);
        var offset = maxOffset == 0 ? 0 : random.Next(0, maxOffset + 1);
        var (sql, parameters) = SqlStatementFactory.BuildRangeQuery(environment, limit, offset);
        return await ExecuteReaderCountAsync(sql, parameters, cancellationToken);
    }

    private async Task<object?> ExecuteScalarAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var connection = await EnsureConnectionOpenAsync(cancellationToken);
            using var command = CreateCommand(connection, sql, parameters);
            return await command.ExecuteScalarAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<int> ExecuteReaderCountAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var connection = await EnsureConnectionOpenAsync(cancellationToken);
            using var command = CreateCommand(connection, sql, parameters);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                rows++;
            }

            return rows;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SqliteConnection> EnsureConnectionOpenAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is { State: System.Data.ConnectionState.Open })
        {
            return _connection;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync(cancellationToken);
        return _connection;
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;

        if (parameters is not null)
        {
            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(
                    parameter.Key.StartsWith('@') ? parameter.Key : $"@{parameter.Key}",
                    parameter.Value ?? DBNull.Value);
            }
        }

        return command;
    }
}

internal static class SqlStatementFactory
{
    public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildPointLookup(
        string environment,
        int startIndex,
        int keyCount)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Project"] = DatabaseSeeder.ProjectName,
            ["Environment"] = environment
        };

        var placeholders = new List<string>(capacity: keyCount);
        for (var index = 0; index < keyCount; index++)
        {
            var name = $"Key{index}";
            parameters[name] = DatabaseSeeder.BuildKey(startIndex + index);
            placeholders.Add($"@{name}");
        }

        var sql =
            $"""
            SELECT Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @Environment COLLATE NOCASE
              AND Key IN ({string.Join(", ", placeholders)})
            ORDER BY Key
            """;

        return (sql, parameters);
    }

    public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildRangeQuery(
        string environment,
        int limit,
        int offset)
    {
        return (
            """
            SELECT Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @Environment COLLATE NOCASE
            ORDER BY Key
            LIMIT @Limit OFFSET @Offset
            """,
            new Dictionary<string, object?>
            {
                ["Project"] = DatabaseSeeder.ProjectName,
                ["Environment"] = environment,
                ["Limit"] = limit,
                ["Offset"] = offset
            });
    }

    public static NelknetLibsqlDatabaseClient CreateDirectClient(
        string dataSource,
        string authToken)
    {
        return new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
        {
            DataSource = dataSource,
            AuthToken = authToken,
            TimeoutSeconds = 60
        }));
    }

    public static NelknetLibsqlDatabaseClient CreateLocalClient(string databasePath)
    {
        return new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
        {
            DataSource = databasePath,
            TimeoutSeconds = 60
        }));
    }

    public static NelknetLibsqlDatabaseClient CreateReplicaClient(
        string libsqlUrl,
        string authToken,
        string replicaPath)
    {
        return new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
        {
            DataSource = libsqlUrl,
            AuthToken = authToken,
            EnableLocalReplica = true,
            LocalReplicaPath = replicaPath,
            LocalReplicaSyncIntervalSeconds = 1,
            TimeoutSeconds = 60
        }));
    }
}
