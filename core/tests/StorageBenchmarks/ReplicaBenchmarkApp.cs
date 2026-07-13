using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Options;
using Nona.Libsql;

namespace Nona.StorageBenchmarks;

internal static class ReplicaBenchmarkApp
{
    private const string ProjectName = "bench-project";
    private const string ProjectSlug = "bench-project";
    private const string EnvironmentName = "medium";
    private const string ApiKey = "BENCH-SCOPED-KEY";
    private const int DatasetRows = 1_000;

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            Directory.CreateDirectory(options.OutputDirectory);

            using var cancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationSource.Cancel();
            };

            var migrationsDirectory = Path.Combine(ResolveRepoRoot(), "core", "src", "Infrastructure", "Migrations");
            await using var primary = new ReplicaBenchmarkTarget(
                "primary",
                CreateDirectClient(options.PrimaryUrl, options.AuthToken, options.OperationTimeout));
            await using var replica = new ReplicaBenchmarkTarget(
                "replica",
                CreateDirectClient(options.ReplicaUrl, options.AuthToken, options.OperationTimeout));

            Console.WriteLine("Checking primary and replica connectivity.");
            await primary.ExecuteAsync("SELECT 1", cancellationSource.Token);
            await replica.ExecuteAsync("SELECT 1", cancellationSource.Token);

            if (!options.SkipSeed)
            {
                Console.WriteLine("Seeding primary benchmark dataset.");
                await SeedPrimaryAsync(primary, migrationsDirectory, cancellationSource.Token);
                Console.WriteLine("Waiting for replica to observe seeded dataset.");
                await WaitForReplicaCountAsync(replica, DatasetRows, options.ReplicationTimeout, cancellationSource.Token);
            }

            var readScenarios = CreateReadScenarios();
            var readResults = new List<ReplicaReadResult>();
            var latencySamples = new List<ReplicaLatencySample>();
            var errorSamples = new List<ReplicaErrorSample>();

            foreach (var target in new[] { primary, replica })
            {
                foreach (var scenario in readScenarios)
                {
                    Console.WriteLine($"Running {options.ClientLabel} / {target.Name} / {scenario.Name}");
                    var execution = await RunReadScenarioAsync(
                        target,
                        scenario,
                        options,
                        cancellationSource.Token);
                    readResults.Add(execution.Result);
                    latencySamples.AddRange(execution.Samples);
                    errorSamples.AddRange(execution.Errors);
                }
            }

            Console.WriteLine("Running replication lag checks.");
            var lagResults = new List<ReplicationLagResult>
            {
                await MeasureSingleInsertLagAsync(primary, replica, options, cancellationSource.Token),
                await MeasureBatchInsertLagAsync(primary, replica, options, cancellationSource.Token)
            };

            Console.WriteLine("Running failover behavior check.");
            var failoverResults = new List<FailoverResult>
            {
                await MeasureFailoverAsync(options, cancellationSource.Token)
            };

            var summary = new ReplicaBenchmarkSummary(
                DateTime.UtcNow,
                options.ClientLabel,
                Environment.MachineName,
                RuntimeInformation.OSDescription,
                Environment.ProcessorCount,
                Environment.Version.ToString(),
                options.PrimaryUrl,
                options.ReplicaUrl,
                readResults,
                latencySamples,
                errorSamples,
                lagResults,
                failoverResults);

            await ReplicaReportWriter.WriteAsync(options.OutputDirectory, summary, cancellationSource.Token);
            Console.WriteLine($"Artifacts written to {options.OutputDirectory}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Replica benchmark cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<ReadScenarioExecution> RunReadScenarioAsync(
        ReplicaBenchmarkTarget target,
        ReplicaReadScenario scenario,
        ReplicaBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        await RunReadPhaseAsync(target, scenario, options.WarmupDuration, options.OperationTimeout, measure: false, cancellationToken);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return await RunReadPhaseAsync(target, scenario, options.MeasurementDuration, options.OperationTimeout, measure: true, cancellationToken);
    }

    private static async Task<ReadScenarioExecution> RunReadPhaseAsync(
        ReplicaBenchmarkTarget target,
        ReplicaReadScenario scenario,
        TimeSpan duration,
        TimeSpan operationTimeout,
        bool measure,
        CancellationToken cancellationToken)
    {
        var stopAt = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
        var latencies = new ConcurrentBag<double>();
        var errorCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var attempts = 0;
        var successes = 0;
        var failures = 0;
        var timeouts = 0;

        async Task WorkerAsync(int workerIndex)
        {
            var random = new Random(HashCode.Combine(target.Name, scenario.Name, workerIndex, 20260429));
            while (Stopwatch.GetTimestamp() < stopAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref attempts);

                using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                operationCts.CancelAfter(operationTimeout);

                var started = Stopwatch.GetTimestamp();
                try
                {
                    await ExecuteReadAsync(target, scenario, random, operationCts.Token);
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    Interlocked.Increment(ref successes);
                    if (measure)
                    {
                        latencies.Add(elapsed);
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    Interlocked.Increment(ref failures);
                    Interlocked.Increment(ref timeouts);
                    if (measure)
                    {
                        latencies.Add(elapsed);
                        errorCounts.AddOrUpdate("timeout", 1, (_, count) => count + 1);
                    }
                }
                catch (Exception ex)
                {
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    Interlocked.Increment(ref failures);
                    if (measure)
                    {
                        latencies.Add(elapsed);
                        errorCounts.AddOrUpdate(TrimError(ex.Message), 1, (_, count) => count + 1);
                    }
                }
            }
        }

        await Task.WhenAll(Enumerable.Range(0, scenario.Concurrency).Select(WorkerAsync));

        if (!measure)
        {
            return new ReadScenarioExecution(
                new ReplicaReadResult(target.Name, scenario.Name, scenario.Operation, scenario.ItemCount, scenario.Concurrency, 0, 0, 0, 0, 0, 0, null, null, null, null, 0),
                [],
                []);
        }

        var ordered = latencies.OrderBy(value => value).ToArray();
        var result = new ReplicaReadResult(
            target.Name,
            scenario.Name,
            scenario.Operation,
            scenario.ItemCount,
            scenario.Concurrency,
            attempts,
            successes,
            failures,
            timeouts,
            attempts == 0 ? 0 : failures * 100d / attempts,
            successes / Math.Max(duration.TotalSeconds, 0.001),
            ordered.Length == 0 ? null : ordered.Average(),
            PercentileOrNull(ordered, 0.50),
            PercentileOrNull(ordered, 0.95),
            PercentileOrNull(ordered, 0.99),
            ordered.Length);

        return new ReadScenarioExecution(
            result,
            ordered.Select(latency => new ReplicaLatencySample(target.Name, scenario.Name, latency)).ToArray(),
            errorCounts
                .OrderByDescending(pair => pair.Value)
                .Select(pair => new ReplicaErrorSample(target.Name, scenario.Name, pair.Key, pair.Value))
                .ToArray());
    }

    private static async Task ExecuteReadAsync(
        ReplicaBenchmarkTarget target,
        ReplicaReadScenario scenario,
        Random random,
        CancellationToken cancellationToken)
    {
        var startIndex = random.Next(1, DatasetRows - scenario.ItemCount + 2);
        var (sql, parameters) = scenario.Operation switch
        {
            ReplicaReadOperation.KeyLookup => BuildPointLookup(startIndex, scenario.ItemCount),
            ReplicaReadOperation.ListQuery => BuildRangeQuery(scenario.ItemCount, random.Next(0, DatasetRows - scenario.ItemCount + 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario.Operation), scenario.Operation, null)
        };

        var result = await target.ExecuteAsync(sql, parameters, cancellationToken);
        if (result.Rows.Count != scenario.ItemCount)
        {
            throw new InvalidOperationException(
                $"Expected {scenario.ItemCount} rows for {scenario.Name}, received {result.Rows.Count}.");
        }
    }

    private static async Task<ReplicationLagResult> MeasureSingleInsertLagAsync(
        ReplicaBenchmarkTarget primary,
        ReplicaBenchmarkTarget replica,
        ReplicaBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        var key = $"REPLICA_LAG_SINGLE_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var insertStarted = Stopwatch.GetTimestamp();
        await primary.ExecuteAsync(
            """
            INSERT OR REPLACE INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt)
            VALUES (@Project, @Environment, @Key, @Value, 'string', 3, @Now, @Now)
            """,
            new
            {
                Project = ProjectName,
                Environment = EnvironmentName,
                Key = key,
                Value = "single-lag-check",
                Now = now
            },
            cancellationToken);

        var immediate = await CountByKeyAsync(replica, key, cancellationToken);
        var observed = await PollUntilAsync(
            () => CountByKeyAsync(replica, key, cancellationToken),
            count => count >= 1,
            options.ReplicationTimeout,
            cancellationToken);

        return new ReplicationLagResult(
            "single-insert-read-by-key",
            Stopwatch.GetElapsedTime(insertStarted).TotalMilliseconds,
            immediate >= 1 ? 0 : 1,
            immediate >= 1 ? 0 : 100,
            observed.Observed,
            observed.ElapsedMs);
    }

    private static async Task<ReplicationLagResult> MeasureBatchInsertLagAsync(
        ReplicaBenchmarkTarget primary,
        ReplicaBenchmarkTarget replica,
        ReplicaBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        var prefix = $"REPLICA_LAG_BATCH_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_";
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var batch = Enumerable.Range(1, batchSize)
            .Select(index => new LibsqlStatement(
                """
                INSERT OR REPLACE INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt)
                VALUES (@Project, @Environment, @Key, @Value, 'string', 3, @Now, @Now)
                """,
                new
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    Key = $"{prefix}{index:D4}",
                    Value = $"batch-lag-check-{index:D4}",
                    Now = now
                }))
            .ToArray();

        var insertStarted = Stopwatch.GetTimestamp();
        foreach (var statement in batch)
        {
            await ExecuteWithRetryAsync(primary, statement.Sql, statement.Parameters!, cancellationToken);
        }

        var immediate = await CountByPrefixAsync(replica, prefix, cancellationToken);
        var observed = await PollUntilAsync(
            () => CountByPrefixAsync(replica, prefix, cancellationToken),
            count => count >= batchSize,
            options.ReplicationTimeout,
            cancellationToken);

        return new ReplicationLagResult(
            "batch-insert-list-query",
            Stopwatch.GetElapsedTime(insertStarted).TotalMilliseconds,
            immediate >= batchSize ? 0 : 1,
            immediate >= batchSize ? 0 : 100,
            observed.Observed,
            observed.ElapsedMs);
    }

    private static async Task<FailoverResult> MeasureFailoverAsync(
        ReplicaBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        using var unavailableReplica = CreateDirectClient(
            options.UnavailableReplicaUrl,
            options.AuthToken,
            TimeSpan.FromSeconds(Math.Min(options.OperationTimeout.TotalSeconds, 3)));
        using var primary = CreateDirectClient(options.PrimaryUrl, options.AuthToken, options.OperationTimeout);

        var started = Stopwatch.GetTimestamp();
        var fellBack = false;
        string? error = null;

        try
        {
            await unavailableReplica.ExecuteAsync("SELECT 1", ct: cancellationToken);
        }
        catch (Exception ex)
        {
            fellBack = true;
            error = TrimError(ex.Message);
            await primary.ExecuteAsync("SELECT 1", ct: cancellationToken);
        }

        return new FailoverResult(
            "unavailable-replica-read",
            options.UnavailableReplicaUrl,
            fellBack,
            error,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private static async Task SeedPrimaryAsync(
        ReplicaBenchmarkTarget primary,
        string migrationsDirectory,
        CancellationToken cancellationToken)
    {
        var migrationRunner = new LibsqlMigrationRunner(primary.Client, migrationsDirectory);
        await migrationRunner.RunMigrationsAsync(cancellationToken);

        await ExecuteBatchWithRetryAsync(primary,
        [
            new("DELETE FROM ConfigEntries WHERE Project = @Project", new { Project = ProjectName }),
            new("DELETE FROM ApiKeys WHERE Project = @Project", new { Project = ProjectName }),
            new("DELETE FROM Environments WHERE Project = @Project", new { Project = ProjectName }),
            new("DELETE FROM Projects WHERE Name = @Name OR UrlSlug = @Slug", new { Name = ProjectName, Slug = ProjectSlug })
        ], cancellationToken);

        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        await ExecuteBatchWithRetryAsync(primary,
        [
            new(
                """
                INSERT INTO Projects (Name, UrlSlug, CreatedAt, UpdatedAt)
                VALUES (@Name, @Slug, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    Name = ProjectName,
                    Slug = ProjectSlug,
                    CreatedAt = now,
                    UpdatedAt = now
                }),
            new(
                """
                INSERT INTO ApiKeys (Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt)
                VALUES (@Name, @Key, @Project, NULL, @Scope, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    Name = "Benchmark",
                    Key = ApiKey,
                    Project = ProjectName,
                    Scope = 3,
                    CreatedAt = now,
                    UpdatedAt = now
                }),
            new(
                """
                INSERT INTO Environments (Name, Project, CreatedAt, UpdatedAt)
                VALUES (@Name, @Project, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    Name = EnvironmentName,
                    Project = ProjectName,
                    CreatedAt = now,
                    UpdatedAt = now
                })
        ], cancellationToken);

        for (var index = 1; index <= DatasetRows; index++)
        {
            await ExecuteWithRetryAsync(
                primary,
                """
                INSERT INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt)
                VALUES (@Project, @Environment, @Key, @Value, 'string', 3, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    Key = BuildKey(index),
                    Value = $"medium-value-{index:D7}-abcdefghijklmnopqrstuvwxyz0123456789",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                cancellationToken);
        }
    }

    private static async Task ExecuteWithRetryAsync(
        ReplicaBenchmarkTarget target,
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await target.ExecuteAsync(sql, parameters, cancellationToken);
                return;
            }
            catch (LibsqlException ex) when (attempt < 10 && ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt * attempt), cancellationToken);
            }
        }
    }

    private static async Task ExecuteBatchWithRetryAsync(
        ReplicaBenchmarkTarget target,
        IReadOnlyList<LibsqlStatement> statements,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await target.ExecuteBatchAsync(statements, cancellationToken);
                return;
            }
            catch (LibsqlException ex) when (attempt < 10 && ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt * attempt), cancellationToken);
            }
        }
    }

    private static async Task WaitForReplicaCountAsync(
        ReplicaBenchmarkTarget replica,
        int expectedRows,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var observed = await PollUntilAsync(
            () => CountDatasetRowsAsync(replica, cancellationToken),
            count => count >= expectedRows,
            timeout,
            cancellationToken);

        if (!observed.Observed)
        {
            throw new TimeoutException(
                $"Replica did not reach {expectedRows} seeded rows within {timeout.TotalSeconds:F1}s. Last count={observed.LastValue}.");
        }
    }

    private static async Task<PollResult> PollUntilAsync(
        Func<Task<int>> getValue,
        Func<int, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var lastValue = 0;

        while (Stopwatch.GetElapsedTime(started) < timeout)
        {
            lastValue = await getValue();
            if (predicate(lastValue))
            {
                return new PollResult(true, Stopwatch.GetElapsedTime(started).TotalMilliseconds, lastValue);
            }

            await Task.Delay(25, cancellationToken);
        }

        return new PollResult(false, timeout.TotalMilliseconds, lastValue);
    }

    private static async Task<int> CountDatasetRowsAsync(ReplicaBenchmarkTarget target, CancellationToken cancellationToken)
    {
        var result = await target.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM ConfigEntries
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @Environment COLLATE NOCASE
              AND Key LIKE 'KEY_%'
            """,
            new { Project = ProjectName, Environment = EnvironmentName },
            cancellationToken);
        return result.Rows[0].GetInt32(0);
    }

    private static async Task<int> CountByKeyAsync(
        ReplicaBenchmarkTarget target,
        string key,
        CancellationToken cancellationToken)
    {
        var result = await target.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM ConfigEntries
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @Environment COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
            """,
            new { Project = ProjectName, Environment = EnvironmentName, Key = key },
            cancellationToken);
        return result.Rows[0].GetInt32(0);
    }

    private static async Task<int> CountByPrefixAsync(
        ReplicaBenchmarkTarget target,
        string prefix,
        CancellationToken cancellationToken)
    {
        var result = await target.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM ConfigEntries
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @Environment COLLATE NOCASE
              AND Key LIKE @Prefix
            """,
            new { Project = ProjectName, Environment = EnvironmentName, Prefix = $"{prefix}%" },
            cancellationToken);
        return result.Rows[0].GetInt32(0);
    }

    private static (string Sql, object Parameters) BuildPointLookup(int startIndex, int keyCount)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Project"] = ProjectName,
            ["Environment"] = EnvironmentName
        };
        var placeholders = new List<string>(keyCount);

        for (var index = 0; index < keyCount; index++)
        {
            placeholders.Add($"'{EscapeSqlLiteral(BuildKey(startIndex + index))}'");
        }

        return (
            $"""
            SELECT Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @Environment COLLATE NOCASE
              AND Key IN ({string.Join(", ", placeholders)})
            ORDER BY Key
            """,
            parameters);
    }

    private static string EscapeSqlLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static (string Sql, object Parameters) BuildRangeQuery(int limit, int offset)
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
            new
            {
                Project = ProjectName,
                Environment = EnvironmentName,
                Limit = limit,
                Offset = offset
            });
    }

    private static IReadOnlyList<ReplicaReadScenario> CreateReadScenarios()
    {
        var result = new List<ReplicaReadScenario>();
        foreach (var concurrency in new[] { 1, 10, 50, 100 })
        {
            result.Add(new($"key-1-c{concurrency}", ReplicaReadOperation.KeyLookup, 1, concurrency));
            result.Add(new($"key-100-c{concurrency}", ReplicaReadOperation.KeyLookup, 100, concurrency));
            result.Add(new($"list-1000-c{concurrency}", ReplicaReadOperation.ListQuery, 1000, concurrency));
        }

        return result;
    }

    private static NelknetLibsqlDatabaseClient CreateDirectClient(
        string url,
        string authToken,
        TimeSpan timeout)
    {
        return new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
        {
            DataSource = url,
            AuthToken = authToken,
            TimeoutSeconds = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds))
        }));
    }

    private static ReplicaBenchmarkOptions ParseOptions(string[] args)
    {
        string? primaryUrl = null;
        string? replicaUrl = null;
        string? outputDirectory = null;
        var authToken = string.Empty;
        var clientLabel = Environment.MachineName;
        var warmupSeconds = 1d;
        var measurementSeconds = 4d;
        var timeoutSeconds = 10d;
        var replicationTimeoutSeconds = 10d;
        var unavailableReplicaUrl = "http://127.0.0.1:1";
        var skipSeed = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--primary-url":
                    primaryUrl = args[++index];
                    break;
                case "--replica-url":
                    replicaUrl = args[++index];
                    break;
                case "--auth-token":
                    authToken = args[++index];
                    break;
                case "--output":
                    outputDirectory = args[++index];
                    break;
                case "--client-label":
                    clientLabel = args[++index];
                    break;
                case "--warmup-seconds":
                    warmupSeconds = double.Parse(args[++index], CultureInfo.InvariantCulture);
                    break;
                case "--measurement-seconds":
                    measurementSeconds = double.Parse(args[++index], CultureInfo.InvariantCulture);
                    break;
                case "--timeout-seconds":
                    timeoutSeconds = double.Parse(args[++index], CultureInfo.InvariantCulture);
                    break;
                case "--replication-timeout-seconds":
                    replicationTimeoutSeconds = double.Parse(args[++index], CultureInfo.InvariantCulture);
                    break;
                case "--unavailable-replica-url":
                    unavailableReplicaUrl = args[++index];
                    break;
                case "--skip-seed":
                    skipSeed = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'.");
            }
        }

        primaryUrl ??= Environment.GetEnvironmentVariable("NONA_BENCH_PRIMARY_URL");
        replicaUrl ??= Environment.GetEnvironmentVariable("NONA_BENCH_REPLICA_URL");
        authToken = Environment.GetEnvironmentVariable("NONA_BENCH_LIBSQL_AUTH_TOKEN") ?? authToken;

        if (string.IsNullOrWhiteSpace(primaryUrl))
        {
            throw new ArgumentException("--primary-url is required.");
        }

        if (string.IsNullOrWhiteSpace(replicaUrl))
        {
            throw new ArgumentException("--replica-url is required.");
        }

        outputDirectory ??= Path.Combine(
            ResolveRepoRoot(),
            "artifacts",
            "benchmarks",
            $"replica-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{SanitizeFileName(clientLabel)}");

        return new ReplicaBenchmarkOptions(
            Path.GetFullPath(outputDirectory),
            primaryUrl,
            replicaUrl,
            authToken,
            clientLabel,
            TimeSpan.FromSeconds(warmupSeconds),
            TimeSpan.FromSeconds(measurementSeconds),
            TimeSpan.FromSeconds(timeoutSeconds),
            TimeSpan.FromSeconds(replicationTimeoutSeconds),
            unavailableReplicaUrl,
            skipSeed);
    }

    private static string ResolveRepoRoot()
    {
        var candidate = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(candidate))
        {
            if (File.Exists(Path.Combine(candidate, "NonaConfig.slnx")))
            {
                return candidate;
            }

            candidate = Directory.GetParent(candidate)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root.");
    }

    private static string BuildKey(int index) => $"KEY_{index:D7}";

    private static double? PercentileOrNull(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return null;
        }

        if (sortedValues.Length == 1)
        {
            return sortedValues[0];
        }

        var index = (sortedValues.Length - 1) * percentile;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var fraction = index - lower;
        return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction);
    }

    private static string TrimError(string value)
    {
        var normalized = value.Replace(Environment.NewLine, " ").Trim();
        return normalized.Length <= 200 ? normalized : normalized[..200];
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '-' : ch));
    }
}

internal sealed class ReplicaBenchmarkTarget : IAsyncDisposable
{
    public ReplicaBenchmarkTarget(string name, NelknetLibsqlDatabaseClient client)
    {
        Name = name;
        Client = client;
    }

    public string Name { get; }
    public NelknetLibsqlDatabaseClient Client { get; }

    public Task<LibsqlQueryResult> ExecuteAsync(
        string sql,
        CancellationToken cancellationToken)
        => Client.ExecuteAsync(sql, ct: cancellationToken);

    public Task<LibsqlQueryResult> ExecuteAsync(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
        => Client.ExecuteAsync(sql, parameters, cancellationToken);

    public Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
        IEnumerable<LibsqlStatement> statements,
        CancellationToken cancellationToken)
        => Client.ExecuteBatchAsync(statements, cancellationToken);

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal static class ReplicaReportWriter
{
    public static async Task WriteAsync(
        string outputDirectory,
        ReplicaBenchmarkSummary summary,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "read-results.csv"),
            BuildReadResultsCsv(summary.ReadResults),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "latency-samples.csv"),
            BuildLatencySamplesCsv(summary.LatencySamples),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "errors.csv"),
            BuildErrorsCsv(summary.ErrorSamples),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "replication-lag.csv"),
            BuildLagCsv(summary.ReplicationLagResults),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "failover-results.csv"),
            BuildFailoverCsv(summary.FailoverResults),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "replica-benchmark-report.txt"),
            BuildTextReport(summary),
            cancellationToken);
    }

    private static string BuildReadResultsCsv(IReadOnlyList<ReplicaReadResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("target,scenario,operation,item_count,concurrency,attempts,successes,failures,timeouts,error_rate_percent,throughput_per_second,average_latency_ms,p50_latency_ms,p95_latency_ms,p99_latency_ms,sample_count");
        foreach (var result in results)
        {
            AppendCsvLine(sb,
            [
                result.Target,
                result.Scenario,
                result.Operation.ToString(),
                result.ItemCount.ToString(CultureInfo.InvariantCulture),
                result.Concurrency.ToString(CultureInfo.InvariantCulture),
                result.Attempts.ToString(CultureInfo.InvariantCulture),
                result.Successes.ToString(CultureInfo.InvariantCulture),
                result.Failures.ToString(CultureInfo.InvariantCulture),
                result.Timeouts.ToString(CultureInfo.InvariantCulture),
                result.ErrorRatePercent.ToString("F3", CultureInfo.InvariantCulture),
                result.ThroughputPerSecond.ToString("F3", CultureInfo.InvariantCulture),
                FormatNullable(result.AverageLatencyMs),
                FormatNullable(result.P50LatencyMs),
                FormatNullable(result.P95LatencyMs),
                FormatNullable(result.P99LatencyMs),
                result.SampleCount.ToString(CultureInfo.InvariantCulture)
            ]);
        }

        return sb.ToString();
    }

    private static string BuildLatencySamplesCsv(IReadOnlyList<ReplicaLatencySample> samples)
    {
        var sb = new StringBuilder();
        sb.AppendLine("target,scenario,latency_ms");
        foreach (var sample in samples)
        {
            AppendCsvLine(sb,
            [
                sample.Target,
                sample.Scenario,
                sample.LatencyMs.ToString("F3", CultureInfo.InvariantCulture)
            ]);
        }

        return sb.ToString();
    }

    private static string BuildLagCsv(IReadOnlyList<ReplicationLagResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("scenario,write_latency_ms,stale_reads,stale_read_rate_percent,observed,replication_lag_ms");
        foreach (var result in results)
        {
            AppendCsvLine(sb,
            [
                result.Scenario,
                result.WriteLatencyMs.ToString("F3", CultureInfo.InvariantCulture),
                result.StaleReads.ToString(CultureInfo.InvariantCulture),
                result.StaleReadRatePercent.ToString("F3", CultureInfo.InvariantCulture),
                result.Observed.ToString(),
                result.ReplicationLagMs.ToString("F3", CultureInfo.InvariantCulture)
            ]);
        }

        return sb.ToString();
    }

    private static string BuildErrorsCsv(IReadOnlyList<ReplicaErrorSample> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("target,scenario,error,count");
        foreach (var error in errors)
        {
            AppendCsvLine(sb,
            [
                error.Target,
                error.Scenario,
                error.Error,
                error.Count.ToString(CultureInfo.InvariantCulture)
            ]);
        }

        return sb.ToString();
    }

    private static string BuildFailoverCsv(IReadOnlyList<FailoverResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("scenario,unavailable_replica_url,fell_back_to_primary,replica_error,total_latency_ms");
        foreach (var result in results)
        {
            AppendCsvLine(sb,
            [
                result.Scenario,
                result.UnavailableReplicaUrl,
                result.FellBackToPrimary.ToString(),
                result.ReplicaError ?? string.Empty,
                result.TotalLatencyMs.ToString("F3", CultureInfo.InvariantCulture)
            ]);
        }

        return sb.ToString();
    }

    private static string BuildTextReport(ReplicaBenchmarkSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("libSQL Read Replica Benchmark Report");
        sb.AppendLine("====================================");
        sb.AppendLine();
        sb.AppendLine($"Generated UTC: {summary.GeneratedAtUtc:O}");
        sb.AppendLine($"Client label: {summary.ClientLabel}");
        sb.AppendLine($"Machine: {summary.MachineName}");
        sb.AppendLine($"OS: {summary.OsDescription}");
        sb.AppendLine($"CPU logical cores: {summary.ProcessorCount}");
        sb.AppendLine($".NET: {summary.DotnetVersion}");
        sb.AppendLine($"Primary URL: {summary.PrimaryUrl}");
        sb.AppendLine($"Replica URL: {summary.ReplicaUrl}");
        sb.AppendLine();

        sb.AppendLine("Read Latency Summary");
        sb.AppendLine("--------------------");
        sb.AppendLine("scenario | primary p50/p95/p99 ms | replica p50/p95/p99 ms | primary rps | replica rps | replica p95 vs primary");
        var primaryResults = summary.ReadResults.Where(result => result.Target == "primary")
            .ToDictionary(result => result.Scenario, StringComparer.OrdinalIgnoreCase);
        var replicaResults = summary.ReadResults.Where(result => result.Target == "replica")
            .ToDictionary(result => result.Scenario, StringComparer.OrdinalIgnoreCase);

        foreach (var scenario in primaryResults.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            primaryResults.TryGetValue(scenario, out var primary);
            replicaResults.TryGetValue(scenario, out var replica);
            var ratio = primary?.P95LatencyMs is > 0 && replica?.P95LatencyMs is not null
                ? $"{replica.P95LatencyMs.Value / primary.P95LatencyMs.Value:F2}x"
                : "n/a";
            sb.AppendLine(
                $"{scenario} | {Triplet(primary)} | {Triplet(replica)} | {FormatNullable(primary?.ThroughputPerSecond)} | {FormatNullable(replica?.ThroughputPerSecond)} | {ratio}");
        }

        sb.AppendLine();
        sb.AppendLine("Read Error Summary");
        sb.AppendLine("------------------");
        if (summary.ErrorSamples.Count == 0)
        {
            sb.AppendLine("No read scenario errors were captured.");
        }
        else
        {
            foreach (var error in summary.ErrorSamples.OrderBy(error => error.Target).ThenBy(error => error.Scenario))
            {
                sb.AppendLine($"{error.Target} / {error.Scenario}: {error.Count} x {error.Error}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Replication Lag / Consistency");
        sb.AppendLine("-----------------------------");
        foreach (var result in summary.ReplicationLagResults)
        {
            sb.AppendLine(
                $"{result.Scenario}: lag={result.ReplicationLagMs:F3} ms, observed={result.Observed}, stale_read_rate={result.StaleReadRatePercent:F3}%, write_latency={result.WriteLatencyMs:F3} ms");
        }

        sb.AppendLine();
        sb.AppendLine("Failover / Fallback");
        sb.AppendLine("-------------------");
        foreach (var result in summary.FailoverResults)
        {
            sb.AppendLine(
                $"{result.Scenario}: fallback_to_primary={result.FellBackToPrimary}, latency={result.TotalLatencyMs:F3} ms, replica_error={result.ReplicaError ?? "none"}");
        }

        sb.AppendLine();
        sb.AppendLine("Target Evaluation");
        sb.AppendLine("-----------------");
        var maxError = summary.ReadResults.Select(result => result.ErrorRatePercent).DefaultIfEmpty(0).Max();
        var maxLag = summary.ReplicationLagResults.Select(result => result.ReplicationLagMs).DefaultIfEmpty(0).Max();
        var maxStale = summary.ReplicationLagResults.Select(result => result.StaleReadRatePercent).DefaultIfEmpty(0).Max();
        sb.AppendLine($"Error rate target < 0.5%: {(maxError < 0.5 ? "PASS" : "FAIL")} (max {maxError:F3}%)");
        sb.AppendLine($"Replication lag target <= 500 ms: {(maxLag <= 500 ? "PASS" : "FAIL")} (max {maxLag:F3} ms)");
        sb.AppendLine($"Stale read target <= 1%: {(maxStale <= 1 ? "PASS" : "FAIL")} (max {maxStale:F3}%)");
        sb.AppendLine();

        sb.AppendLine("Recommendations");
        sb.AppendLine("---------------");
        var comparable = primaryResults.Values
            .Join(replicaResults.Values, left => left.Scenario, right => right.Scenario, (left, right) => new { Primary = left, Replica = right })
            .Where(pair => pair.Primary.P95LatencyMs is > 0 && pair.Replica.P95LatencyMs.HasValue)
            .ToArray();
        var replicaWins = comparable.Count(pair => pair.Replica.P95LatencyMs!.Value <= pair.Primary.P95LatencyMs!.Value);
        sb.AppendLine(replicaWins >= Math.Ceiling(comparable.Length / 2d)
            ? "Route read-only API traffic to the nearest healthy replica when read-after-write freshness is not required."
            : "Do not route reads to this replica by default for this client location; primary was equal or faster in most scenarios.");
        sb.AppendLine(maxLag <= 500 && maxStale <= 1
            ? "Use primary routing for writes and read-after-write flows; replica routing is acceptable for eventually consistent reads."
            : "Keep read-after-write and freshness-sensitive flows on primary until replica lag/staleness is reduced.");
        sb.AppendLine("Fallback should be explicit in routing code: on replica connection failure, retry against primary and record the latency penalty.");

        return sb.ToString();
    }

    private static string Triplet(ReplicaReadResult? result)
        => result is null
            ? "n/a"
            : $"{FormatNullable(result.P50LatencyMs)}/{FormatNullable(result.P95LatencyMs)}/{FormatNullable(result.P99LatencyMs)}";

    private static void AppendCsvLine(StringBuilder sb, IEnumerable<string> values)
        => sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value}\"" : value;
    }

    private static string FormatNullable(double? value)
        => value.HasValue ? value.Value.ToString("F3", CultureInfo.InvariantCulture) : "n/a";
}

internal sealed record ReplicaBenchmarkOptions(
    string OutputDirectory,
    string PrimaryUrl,
    string ReplicaUrl,
    string AuthToken,
    string ClientLabel,
    TimeSpan WarmupDuration,
    TimeSpan MeasurementDuration,
    TimeSpan OperationTimeout,
    TimeSpan ReplicationTimeout,
    string UnavailableReplicaUrl,
    bool SkipSeed);

internal enum ReplicaReadOperation
{
    KeyLookup,
    ListQuery
}

internal sealed record ReplicaReadScenario(
    string Name,
    ReplicaReadOperation Operation,
    int ItemCount,
    int Concurrency);

internal sealed record ReplicaReadResult(
    string Target,
    string Scenario,
    ReplicaReadOperation Operation,
    int ItemCount,
    int Concurrency,
    int Attempts,
    int Successes,
    int Failures,
    int Timeouts,
    double ErrorRatePercent,
    double ThroughputPerSecond,
    double? AverageLatencyMs,
    double? P50LatencyMs,
    double? P95LatencyMs,
    double? P99LatencyMs,
    int SampleCount);

internal sealed record ReplicaLatencySample(
    string Target,
    string Scenario,
    double LatencyMs);

internal sealed record ReplicaErrorSample(
    string Target,
    string Scenario,
    string Error,
    int Count);

internal sealed record ReplicationLagResult(
    string Scenario,
    double WriteLatencyMs,
    int StaleReads,
    double StaleReadRatePercent,
    bool Observed,
    double ReplicationLagMs);

internal sealed record FailoverResult(
    string Scenario,
    string UnavailableReplicaUrl,
    bool FellBackToPrimary,
    string? ReplicaError,
    double TotalLatencyMs);

internal sealed record ReplicaBenchmarkSummary(
    DateTime GeneratedAtUtc,
    string ClientLabel,
    string MachineName,
    string OsDescription,
    int ProcessorCount,
    string DotnetVersion,
    string PrimaryUrl,
    string ReplicaUrl,
    IReadOnlyList<ReplicaReadResult> ReadResults,
    IReadOnlyList<ReplicaLatencySample> LatencySamples,
    IReadOnlyList<ReplicaErrorSample> ErrorSamples,
    IReadOnlyList<ReplicationLagResult> ReplicationLagResults,
    IReadOnlyList<FailoverResult> FailoverResults);

internal sealed record ReadScenarioExecution(
    ReplicaReadResult Result,
    IReadOnlyList<ReplicaLatencySample> Samples,
    IReadOnlyList<ReplicaErrorSample> Errors);

internal sealed record PollResult(
    bool Observed,
    double ElapsedMs,
    int LastValue);
