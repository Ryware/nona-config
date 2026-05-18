namespace Nona.StorageBenchmarks;

internal enum DatasetSize
{
    Small,
    Medium,
    Large
}

internal enum WorkloadKind
{
    PointLookup,
    RangeQuery
}

internal sealed record BenchmarkOptions(
    string OutputDirectory,
    TimeSpan WarmupDuration,
    TimeSpan MeasurementDuration,
    TimeSpan OperationTimeout,
    bool IncludePrimaryDiagnostic,
    string? LibsqlUrl,
    string LibsqlAuthToken);

internal sealed record BenchmarkScenario(
    string Name,
    DatasetSize Dataset,
    WorkloadKind Workload,
    int ItemCount,
    int Concurrency,
    bool Required,
    bool RunPrimaryDiagnostic = false);

internal sealed record BenchmarkEnvironmentContext(
    string RepoRoot,
    string OutputDirectory,
    string SeedDatabasePath,
    string SqliteDatabasePath,
    string LibsqlLocalDatabasePath,
    string LibsqlReplicaLocalPath,
    string MigrationsDirectory,
    string? LibsqlUrl,
    string LibsqlAuthToken);

internal sealed record BenchmarkRunSummary(
    DateTime GeneratedAtUtc,
    string MachineName,
    string OsDescription,
    int ProcessorCount,
    string DotnetVersion,
    BenchmarkOptions Options,
    IReadOnlyList<BenchmarkScenario> Scenarios,
    IReadOnlyDictionary<string, int> DatasetRows,
    IReadOnlyList<BenchmarkResult> Results,
    IReadOnlyList<LatencySample> LatencySamples,
    IReadOnlyList<ErrorSample> ErrorSamples);

internal sealed record BenchmarkResult(
    string Provider,
    string Scenario,
    DatasetSize Dataset,
    WorkloadKind Workload,
    int ItemCount,
    int Concurrency,
    bool Required,
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
    int SampleCount,
    bool MeetsErrorTarget,
    bool MeetsP95Target,
    string? TargetDescription,
    string? Note);

internal sealed record LatencySample(
    string Provider,
    string Scenario,
    double LatencyMs);

internal sealed record ErrorSample(
    string Provider,
    string Scenario,
    string Error,
    int Count);

internal sealed record ScenarioExecutionResult(
    BenchmarkResult Result,
    IReadOnlyList<LatencySample> LatencySamples,
    IReadOnlyList<ErrorSample> ErrorSamples);
