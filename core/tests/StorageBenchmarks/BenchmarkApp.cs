using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace Nona.StorageBenchmarks;

internal static class StorageBenchmarkApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            var outputDirectory = Path.GetFullPath(options.OutputDirectory);
            Directory.CreateDirectory(outputDirectory);

            using var cancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationSource.Cancel();
            };

            var environment = await PrepareEnvironmentAsync(options, outputDirectory, cancellationSource.Token);

            await using var sqlite = new SqliteBenchmarkDatabase(
                "sqlite",
                environment.SqliteDatabasePath);

            await using var sqliteClient = new DatabaseClientBenchmarkDatabase(
                "sqlite-client",
                SqlStatementFactory.CreateLocalClient(environment.SqliteClientDatabasePath));

            DatabaseClientBenchmarkDatabase? libsqlReplica = null;
            DatabaseClientBenchmarkDatabase? libsqlPrimary = null;
            if (!string.IsNullOrWhiteSpace(environment.LibsqlUrl))
            {
                libsqlReplica = new DatabaseClientBenchmarkDatabase(
                    "libsql-replica",
                    SqlStatementFactory.CreateReplicaClient(
                        environment.LibsqlUrl,
                        environment.LibsqlAuthToken,
                        environment.LibsqlReplicaLocalPath));

                if (options.IncludePrimaryDiagnostic)
                {
                    libsqlPrimary = new DatabaseClientBenchmarkDatabase(
                        "libsql-primary",
                        SqlStatementFactory.CreateDirectClient(environment.LibsqlUrl, environment.LibsqlAuthToken));
                }
            }

            try
            {
                await sqlite.InitializeAsync(cancellationSource.Token);
                await sqliteClient.InitializeAsync(cancellationSource.Token);
                if (libsqlReplica is not null)
                {
                    await libsqlReplica.InitializeAsync(cancellationSource.Token);
                }
                if (libsqlPrimary is not null)
                {
                    await libsqlPrimary.InitializeAsync(cancellationSource.Token);
                }

                var scenarios = CreateScenarios();
                var results = new List<BenchmarkResult>();
                var latencySamples = new List<LatencySample>();
                var errorSamples = new List<ErrorSample>();

                foreach (var provider in EnumerateProviders(sqlite, sqliteClient, libsqlReplica, libsqlPrimary))
                {
                    foreach (var scenario in scenarios)
                    {
                        if (provider.ProviderName == "libsql-primary" && !scenario.RunPrimaryDiagnostic)
                        {
                            continue;
                        }

                        Console.WriteLine($"Running {provider.ProviderName} / {scenario.Name}");
                        var execution = await RunScenarioAsync(provider, scenario, options, cancellationSource.Token);
                        results.Add(execution.Result);
                        latencySamples.AddRange(execution.LatencySamples);
                        errorSamples.AddRange(execution.ErrorSamples);
                    }
                }

                var summary = new BenchmarkRunSummary(
                    DateTime.UtcNow,
                    Environment.MachineName,
                    RuntimeInformation.OSDescription,
                    Environment.ProcessorCount,
                    Environment.Version.ToString(),
                    options,
                    scenarios,
                    DatabaseSeeder.DatasetRows.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                    results,
                    latencySamples,
                    errorSamples);

                await ReportWriter.WriteAsync(environment, summary, cancellationSource.Token);
                Console.WriteLine($"Artifacts written to {environment.OutputDirectory}");
                return 0;
            }
            finally
            {
                if (libsqlPrimary is not null)
                {
                    await libsqlPrimary.DisposeAsync();
                }

                if (libsqlReplica is not null)
                {
                    await libsqlReplica.DisposeAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Benchmark cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<BenchmarkEnvironmentContext> PrepareEnvironmentAsync(
        BenchmarkOptions options,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var repoRoot = ResolveRepoRoot();
        var benchmarkDataDirectory = Path.Combine(outputDirectory, "data");
        Directory.CreateDirectory(benchmarkDataDirectory);

        var seedDatabasePath = Path.Combine(benchmarkDataDirectory, "seed-base.db");
        var sqliteDatabasePath = Path.Combine(benchmarkDataDirectory, "sqlite.db");
        var sqliteClientDatabasePath = Path.Combine(benchmarkDataDirectory, "sqlite-client.db");
        var libsqlReplicaLocalPath = Path.Combine(benchmarkDataDirectory, "libsql-replica-local.db");
        var migrationsDirectory = Path.Combine(repoRoot, "core", "src", "Infrastructure", "Migrations");

        Console.WriteLine("Creating seeded local SQLite database.");
        await DatabaseSeeder.CreateSeedDatabaseAsync(seedDatabasePath, migrationsDirectory, cancellationToken);
        DatabaseSeeder.CopySeedDatabase(seedDatabasePath, sqliteDatabasePath);
        DatabaseSeeder.CopySeedDatabase(seedDatabasePath, sqliteClientDatabasePath);

        if (File.Exists(libsqlReplicaLocalPath))
        {
            File.Delete(libsqlReplicaLocalPath);
        }

        if (!string.IsNullOrWhiteSpace(options.LibsqlUrl))
        {
            Console.WriteLine("Seeding libsql primary.");
            using var directClient = SqlStatementFactory.CreateDirectClient(options.LibsqlUrl, options.LibsqlAuthToken);
            await DatabaseSeeder.SeedLibsqlDatabaseAsync(directClient, migrationsDirectory, cancellationToken);
        }

        return new BenchmarkEnvironmentContext(
            repoRoot,
            outputDirectory,
            seedDatabasePath,
            sqliteDatabasePath,
            sqliteClientDatabasePath,
            libsqlReplicaLocalPath,
            migrationsDirectory,
            options.LibsqlUrl,
            options.LibsqlAuthToken);
    }

    private static async Task<ScenarioExecutionResult> RunScenarioAsync(
        IBenchmarkDatabase database,
        BenchmarkScenario scenario,
        BenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        await RunPhaseAsync(database, scenario, options.WarmupDuration, options.OperationTimeout, measure: false, cancellationToken);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return await RunPhaseAsync(database, scenario, options.MeasurementDuration, options.OperationTimeout, measure: true, cancellationToken);
    }

    private static async Task<ScenarioExecutionResult> RunPhaseAsync(
        IBenchmarkDatabase database,
        BenchmarkScenario scenario,
        TimeSpan phaseDuration,
        TimeSpan operationTimeout,
        bool measure,
        CancellationToken cancellationToken)
    {
        var stopAt = Stopwatch.GetTimestamp() + (long)(phaseDuration.TotalSeconds * Stopwatch.Frequency);
        var attemptLatencies = new ConcurrentBag<double>();
        var errorCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var attempts = 0;
        var successes = 0;
        var failures = 0;
        var timeouts = 0;

        async Task WorkerAsync(int workerIndex)
        {
            var random = new Random(HashCode.Combine(database.ProviderName, scenario.Name, workerIndex, 20260415));

            while (Stopwatch.GetTimestamp() < stopAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref attempts);

                using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                operationCts.CancelAfter(operationTimeout);

                var started = Stopwatch.GetTimestamp();

                try
                {
                    await database.ExecuteAsync(scenario, random, operationCts.Token);
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    Interlocked.Increment(ref successes);
                    if (measure)
                    {
                        attemptLatencies.Add(elapsed);
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    Interlocked.Increment(ref failures);
                    Interlocked.Increment(ref timeouts);
                    if (measure)
                    {
                        attemptLatencies.Add(elapsed);
                        errorCounts.AddOrUpdate("timeout", 1, (_, count) => count + 1);
                    }
                }
                catch (Exception ex)
                {
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    Interlocked.Increment(ref failures);
                    if (measure)
                    {
                        attemptLatencies.Add(elapsed);
                        errorCounts.AddOrUpdate(TrimError(ex.Message), 1, (_, count) => count + 1);
                    }
                }
            }
        }

        await Task.WhenAll(Enumerable.Range(0, scenario.Concurrency).Select(WorkerAsync));

        if (!measure)
        {
            return new ScenarioExecutionResult(
                new BenchmarkResult(
                    database.ProviderName,
                    scenario.Name,
                    scenario.Dataset,
                    scenario.Workload,
                    scenario.ItemCount,
                    scenario.Concurrency,
                    scenario.Required,
                    attempts,
                    successes,
                    failures,
                    timeouts,
                    0,
                    0,
                    null,
                    null,
                    null,
                    null,
                    0,
                    true,
                    true,
                    null,
                    null),
                [],
                []);
        }

        var orderedLatencies = attemptLatencies.OrderBy(value => value).ToArray();
        var errorRate = attempts == 0 ? 0 : failures * 100d / attempts;
        var p95 = PercentileOrNull(orderedLatencies, 0.95);

        var result = new BenchmarkResult(
            database.ProviderName,
            scenario.Name,
            scenario.Dataset,
            scenario.Workload,
            scenario.ItemCount,
            scenario.Concurrency,
            scenario.Required,
            attempts,
            successes,
            failures,
            timeouts,
            errorRate,
            successes / Math.Max(phaseDuration.TotalSeconds, 0.001),
            orderedLatencies.Length == 0 ? null : orderedLatencies.Average(),
            PercentileOrNull(orderedLatencies, 0.50),
            p95,
            PercentileOrNull(orderedLatencies, 0.99),
            orderedLatencies.Length,
            errorRate < 2,
            MeetsP95Target(scenario, p95),
            GetTargetDescription(scenario),
            failures == attempts && attempts > 0
                ? "All measured attempts failed. Latency percentiles reflect failed/timeout attempt duration."
                : failures > 0
                    ? "Latency percentiles include failed/timeout attempt duration."
                    : null);

        return new ScenarioExecutionResult(
            result,
            orderedLatencies.Select(latency => new LatencySample(database.ProviderName, scenario.Name, latency)).ToArray(),
            errorCounts
                .OrderByDescending(pair => pair.Value)
                .Take(10)
                .Select(pair => new ErrorSample(database.ProviderName, scenario.Name, pair.Key, pair.Value))
                .ToArray());
    }

    private static bool MeetsP95Target(BenchmarkScenario scenario, double? p95)
    {
        if (!p95.HasValue)
        {
            return false;
        }

        return scenario switch
        {
            { Workload: WorkloadKind.PointLookup or WorkloadKind.ReleaseEntryPointLookup, ItemCount: 1 } => p95.Value <= 80,
            { Workload: WorkloadKind.RangeQuery, ItemCount: 100 } => p95.Value <= 120,
            { Workload: WorkloadKind.RangeQuery, ItemCount: 1000 } => p95.Value <= 750,
            _ => true
        };
    }

    private static string? GetTargetDescription(BenchmarkScenario scenario)
    {
        return scenario switch
        {
            { Workload: WorkloadKind.PointLookup or WorkloadKind.ReleaseEntryPointLookup, ItemCount: 1 } => "p95 <= 80 ms",
            { Workload: WorkloadKind.RangeQuery, ItemCount: 100 } => "p95 <= 120 ms",
            { Workload: WorkloadKind.RangeQuery, ItemCount: 1000 } => "p95 <= 750 ms",
            _ => null
        };
    }

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

    private static string TrimError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "unknown";
        }

        var normalized = message.Replace(Environment.NewLine, " ").Trim();
        return normalized.Length <= 180 ? normalized : normalized[..180];
    }

    private static IReadOnlyList<BenchmarkScenario> CreateScenarios()
    {
        return
        [
            new("small-point-1-c1", DatasetSize.Small, WorkloadKind.PointLookup, 1, 1, Required: false, RunPrimaryDiagnostic: true),
            new("small-range-1-c1", DatasetSize.Small, WorkloadKind.RangeQuery, 1, 1, Required: false),
            new("medium-point-1-c1", DatasetSize.Medium, WorkloadKind.PointLookup, 1, 1, Required: true, RunPrimaryDiagnostic: true),
            new("medium-point-1-c50", DatasetSize.Medium, WorkloadKind.PointLookup, 1, 50, Required: true, RunPrimaryDiagnostic: true),
            new("medium-point-10-c10", DatasetSize.Medium, WorkloadKind.PointLookup, 10, 10, Required: true, RunPrimaryDiagnostic: true),
            new("medium-point-100-c10", DatasetSize.Medium, WorkloadKind.PointLookup, 100, 10, Required: true, RunPrimaryDiagnostic: true),
            new("medium-release-hydration-point-1-c1", DatasetSize.Medium, WorkloadKind.ReleaseHydrationPointLookup, 1, 1, Required: false),
            new("medium-release-point-1-c1", DatasetSize.Medium, WorkloadKind.ReleaseEntryPointLookup, 1, 1, Required: true, RunPrimaryDiagnostic: true),
            new("medium-release-point-1-c50", DatasetSize.Medium, WorkloadKind.ReleaseEntryPointLookup, 1, 50, Required: true, RunPrimaryDiagnostic: true),
            new("medium-range-100-c10", DatasetSize.Medium, WorkloadKind.RangeQuery, 100, 10, Required: false, RunPrimaryDiagnostic: true),
            new("medium-range-1000-c10", DatasetSize.Medium, WorkloadKind.RangeQuery, 1000, 10, Required: true, RunPrimaryDiagnostic: true),
            new("medium-range-10000-c5", DatasetSize.Medium, WorkloadKind.RangeQuery, 10000, 5, Required: true),
            new("large-point-1-c1", DatasetSize.Large, WorkloadKind.PointLookup, 1, 1, Required: false, RunPrimaryDiagnostic: true),
            new("large-point-1-c50", DatasetSize.Large, WorkloadKind.PointLookup, 1, 50, Required: false),
            new("large-point-100-c10", DatasetSize.Large, WorkloadKind.PointLookup, 100, 10, Required: false),
            new("large-release-point-1-c1", DatasetSize.Large, WorkloadKind.ReleaseEntryPointLookup, 1, 1, Required: false, RunPrimaryDiagnostic: true),
            new("large-release-point-1-c50", DatasetSize.Large, WorkloadKind.ReleaseEntryPointLookup, 1, 50, Required: false),
            new("large-range-10000-c5", DatasetSize.Large, WorkloadKind.RangeQuery, 10000, 5, Required: false)
        ];
    }

    private static IEnumerable<IBenchmarkDatabase> EnumerateProviders(
        IBenchmarkDatabase sqlite,
        IBenchmarkDatabase sqliteClient,
        IBenchmarkDatabase? libsqlReplica,
        IBenchmarkDatabase? libsqlPrimary)
    {
        yield return sqlite;
        yield return sqliteClient;
        if (libsqlReplica is not null)
        {
            yield return libsqlReplica;
        }
        if (libsqlPrimary is not null)
        {
            yield return libsqlPrimary;
        }
    }

    private static BenchmarkOptions ParseOptions(string[] args)
    {
        var repoRoot = ResolveRepoRoot();
        string? outputDirectory = null;
        string? backendConfigPath = null;
        string? libsqlUrl = null;
        string libsqlAuthToken = string.Empty;
        var warmupSeconds = 1.5;
        var measurementSeconds = 6d;
        var operationTimeoutSeconds = 15d;
        var includePrimaryDiagnostic = true;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output":
                    outputDirectory = args[++index];
                    break;
                case "--backend-config":
                    backendConfigPath = args[++index];
                    break;
                case "--warmup-seconds":
                    warmupSeconds = double.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--measurement-seconds":
                    measurementSeconds = double.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--timeout-seconds":
                    operationTimeoutSeconds = double.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--skip-primary-diagnostic":
                    includePrimaryDiagnostic = false;
                    break;
                case "--libsql-url":
                    libsqlUrl = args[++index];
                    break;
                case "--libsql-auth-token":
                    libsqlAuthToken = args[++index];
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'.");
            }
        }

        var backendConfiguration = LoadBackendConfiguration(repoRoot, backendConfigPath);
        var configuredDataSource = backendConfiguration.GetConnectionString("Libsql")
            ?? backendConfiguration["Storage:Libsql:DataSource"];
        var managedPrimaryEnabled = backendConfiguration.GetValue<bool>("Storage:Libsql:ManagedPrimary:Enabled");
        var managedPrimaryLocalConnectUrl = backendConfiguration["Storage:Libsql:ManagedPrimary:LocalConnectUrl"];
        var managedPrimaryListenAddress = backendConfiguration["Storage:Libsql:ManagedPrimary:HttpListenAddress"];

        if (string.IsNullOrWhiteSpace(configuredDataSource) && managedPrimaryEnabled)
        {
            configuredDataSource = !string.IsNullOrWhiteSpace(managedPrimaryLocalConnectUrl)
                ? managedPrimaryLocalConnectUrl
                : ResolveManagedPrimaryConnectUrl(managedPrimaryListenAddress);
        }

        libsqlUrl ??= IsRemoteDataSource(configuredDataSource)
            ? configuredDataSource
            : null;
        libsqlAuthToken = string.IsNullOrWhiteSpace(libsqlAuthToken)
            ? backendConfiguration["Storage:Libsql:AuthToken"] ?? string.Empty
            : libsqlAuthToken;

        libsqlUrl = Environment.GetEnvironmentVariable("NONA_BENCH_LIBSQL_URL") ?? libsqlUrl;
        libsqlAuthToken = Environment.GetEnvironmentVariable("NONA_BENCH_LIBSQL_AUTH_TOKEN") ?? libsqlAuthToken;

        outputDirectory ??= Path.Combine(
            repoRoot,
            "artifacts",
            "benchmarks",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));

        return new BenchmarkOptions(
            outputDirectory,
            TimeSpan.FromSeconds(warmupSeconds),
            TimeSpan.FromSeconds(measurementSeconds),
            TimeSpan.FromSeconds(operationTimeoutSeconds),
            includePrimaryDiagnostic,
            libsqlUrl,
            libsqlAuthToken);
    }

    private static IConfigurationRoot LoadBackendConfiguration(string repoRoot, string? backendConfigPath)
    {
        var webApiDirectory = Path.Combine(repoRoot, "core", "src", "WebApi");
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        var builder = new ConfigurationBuilder()
            .SetBasePath(webApiDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            builder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);
        }

        if (!string.IsNullOrWhiteSpace(backendConfigPath))
        {
            var resolvedPath = Path.IsPathRooted(backendConfigPath)
                ? backendConfigPath
                : Path.GetFullPath(Path.Combine(repoRoot, backendConfigPath));

            builder.AddJsonFile(resolvedPath, optional: false, reloadOnChange: false);
        }

        builder.AddEnvironmentVariables();
        return builder.Build();
    }

    private static bool IsRemoteDataSource(string? dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return false;
        }

        return dataSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveManagedPrimaryConnectUrl(string? listenAddress)
    {
        if (string.IsNullOrWhiteSpace(listenAddress))
        {
            return null;
        }

        var trimmed = listenAddress.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(trimmed, UriKind.Absolute);
            var host = NormalizeManagedPrimaryHost(uri.Host);
            return $"{uri.Scheme}://{host}:{uri.Port}";
        }

        var separatorIndex = trimmed.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
        {
            return null;
        }

        var normalizedHost = NormalizeManagedPrimaryHost(trimmed[..separatorIndex]);
        var port = trimmed[(separatorIndex + 1)..];
        return $"http://{normalizedHost}:{port}";
    }

    private static string NormalizeManagedPrimaryHost(string host)
    {
        return string.IsNullOrWhiteSpace(host) || host is "0.0.0.0" or "*" or "::" or "[::]"
            ? "127.0.0.1"
            : host.Trim('[', ']');
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
}
