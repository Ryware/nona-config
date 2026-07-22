using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nona.StorageBenchmarks;

internal static class ReportWriter
{
    public static async Task WriteAsync(
        BenchmarkEnvironmentContext context,
        BenchmarkRunSummary summary,
        CancellationToken cancellationToken)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        await File.WriteAllTextAsync(
            Path.Combine(context.OutputDirectory, "results.json"),
            JsonSerializer.Serialize(summary, jsonOptions),
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(context.OutputDirectory, "results.csv"),
            BuildResultsCsv(summary.Results),
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(context.OutputDirectory, "latency-samples.csv"),
            BuildLatencySamplesCsv(summary.LatencySamples),
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(context.OutputDirectory, "errors.csv"),
            BuildErrorsCsv(summary.ErrorSamples),
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(context.OutputDirectory, "REPORT.md"),
            BuildMarkdownReport(summary),
            cancellationToken);
    }

    private static string BuildResultsCsv(IReadOnlyList<BenchmarkResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("provider,scenario,dataset,workload,item_count,concurrency,required,attempts,successes,failures,timeouts,error_rate_percent,throughput_per_second,average_latency_ms,p50_latency_ms,p95_latency_ms,p99_latency_ms,sample_count,meets_error_target,meets_p95_target,target_description,note");

        foreach (var result in results)
        {
            AppendCsvLine(sb, new[]
            {
                result.Provider,
                result.Scenario,
                result.Dataset.ToString(),
                result.Workload.ToString(),
                result.ItemCount.ToString(CultureInfo.InvariantCulture),
                result.Concurrency.ToString(CultureInfo.InvariantCulture),
                result.Required.ToString(),
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
                result.SampleCount.ToString(CultureInfo.InvariantCulture),
                result.MeetsErrorTarget.ToString(),
                result.MeetsP95Target.ToString(),
                result.TargetDescription ?? string.Empty,
                result.Note ?? string.Empty
            });
        }

        return sb.ToString();
    }

    private static string BuildLatencySamplesCsv(IReadOnlyList<LatencySample> samples)
    {
        var sb = new StringBuilder();
        sb.AppendLine("provider,scenario,latency_ms");

        foreach (var sample in samples)
        {
            AppendCsvLine(sb, new[]
            {
                sample.Provider,
                sample.Scenario,
                sample.LatencyMs.ToString("F3", CultureInfo.InvariantCulture)
            });
        }

        return sb.ToString();
    }

    private static string BuildErrorsCsv(IReadOnlyList<ErrorSample> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("provider,scenario,error,count");

        foreach (var error in errors)
        {
            AppendCsvLine(sb, new[]
            {
                error.Provider,
                error.Scenario,
                error.Error,
                error.Count.ToString(CultureInfo.InvariantCulture)
            });
        }

        return sb.ToString();
    }

    private static string BuildMarkdownReport(BenchmarkRunSummary summary)
    {
        var sqliteClient = summary.Results.Where(result => result.Provider == "sqlite-client")
            .ToDictionary(result => result.Scenario, StringComparer.OrdinalIgnoreCase);
        var libsqlReplica = summary.Results.Where(result => result.Provider == "libsql-replica")
            .ToDictionary(result => result.Scenario, StringComparer.OrdinalIgnoreCase);
        var libsqlPrimary = summary.Results.Where(result => result.Provider == "libsql-primary")
            .ToDictionary(result => result.Scenario, StringComparer.OrdinalIgnoreCase);

        var severeLocalFailures = summary.Results
            .Where(result => result.Required && result.Provider == "sqlite-client")
            .Where(result => result.Timeouts > 0 || result.ErrorRatePercent >= 2 || !result.MeetsP95Target)
            .ToList();

        var severeReplicaFailures = summary.Results
            .Where(result => result.Required && result.Provider == "libsql-replica")
            .Where(result => result.Timeouts > 0 || result.ErrorRatePercent >= 2 || !result.MeetsP95Target)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Storage Benchmark Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {summary.GeneratedAtUtc:O}");
        sb.AppendLine();
        sb.AppendLine("## Scope");
        sb.AppendLine();
        sb.AppendLine("- Measured `sqlite-client` for single-node mode with repeatable read workloads that mirror app read flow: project lookup, environment check, then point, range, or release-entry query.");
        if (libsqlReplica.Count > 0)
        {
            sb.AppendLine("- Also compared remote `libsql-primary` and embedded `libsql-replica` when external libsql primary was provided.");
        }
        if (libsqlPrimary.Count > 0)
        {
            sb.AppendLine("- Added `libsql-primary` diagnostics to separate direct-primary cost from embedded-replica overhead.");
        }

        sb.AppendLine("- Benchmarks run at storage-provider layer. API/controller overhead is excluded, so measured latency is a lower bound for full request latency.");
        sb.AppendLine();
        sb.AppendLine("## Environment");
        sb.AppendLine();
        sb.AppendLine($"- OS: {summary.OsDescription}");
        sb.AppendLine($"- Machine: {summary.MachineName}");
        sb.AppendLine($"- CPU logical cores: {summary.ProcessorCount}");
        sb.AppendLine($"- .NET: {summary.DotnetVersion}");
        sb.AppendLine($"- Warmup: {summary.Options.WarmupDuration.TotalSeconds:F1}s");
        sb.AppendLine($"- Measurement: {summary.Options.MeasurementDuration.TotalSeconds:F1}s");
        sb.AppendLine($"- Operation timeout: {summary.Options.OperationTimeout.TotalSeconds:F1}s");
        sb.AppendLine($"- Dataset rows: small={summary.DatasetRows["Small"]:N0}, medium={summary.DatasetRows["Medium"]:N0}, large={summary.DatasetRows["Large"]:N0}");
        sb.AppendLine();
        sb.AppendLine("## Main Findings");
        sb.AppendLine();

        if (severeLocalFailures.Count == 0)
        {
            sb.AppendLine("- `sqlite-client` stayed within configured error and p95 targets for all required scenarios.");
        }
        else
        {
            sb.AppendLine("- `sqlite-client` missed required single-node targets.");
        }

        if (libsqlReplica.Count > 0)
        {
            if (severeReplicaFailures.Count == 0)
            {
                sb.AppendLine("- `libsql-replica` stayed within configured error and p95 targets for all required scenarios.");
            }
            else
            {
                sb.AppendLine("- `libsql-replica` missed required distributed-mode targets.");
            }
        }

        if (libsqlPrimary.Count > 0)
        {
            var primaryVsLocalRatio = libsqlPrimary.Values
                .Join(sqliteClient.Values, left => left.Scenario, right => right.Scenario, (left, right) => new { left, right })
                .Where(pair => pair.left.P95LatencyMs.HasValue && pair.right.P95LatencyMs.HasValue)
                .Select(pair => pair.left.P95LatencyMs!.Value / Math.Max(pair.right.P95LatencyMs!.Value, 0.001))
                .DefaultIfEmpty(1d)
                .Average();

            if (primaryVsLocalRatio < 3)
            {
                sb.AppendLine("- `libsql-primary` stayed close to `sqlite-client` in diagnostic runs. Main regression source is replica behavior, not SQL execution itself.");
            }
            else
            {
                sb.AppendLine("- `libsql-primary` also regressed materially. Replica sync is not sole bottleneck.");
            }
        }

        sb.AppendLine("- `sqlite-client` uses Nona's `SqliteDatabaseClient` over a local SQLite file.");
        if (libsqlReplica.Count > 0)
        {
            sb.AppendLine("- `libsql-replica` here is Nelknet embedded replica mode: reads from local replica file, syncs from remote primary on configured interval.");
        }

        sb.AppendLine();
        sb.AppendLine("## Required Matrix: Single Node");
        sb.AppendLine();
        sb.AppendLine("| Scenario | SQLite client p95 ms | SQLite client rps | SQLite client error % | Verdict |");
        sb.AppendLine("|---|---:|---:|---:|---|");

        foreach (var scenario in summary.Scenarios.Where(scenario => scenario.Required))
        {
            sqliteClient.TryGetValue(scenario.Name, out var localResult);

            var verdict = localResult is null
                ? "missing"
                : localResult.Timeouts > 0 || localResult.ErrorRatePercent >= 2 || !localResult.MeetsP95Target
                    ? "fail"
                    : "pass";

            sb.AppendLine(
                $"| {scenario.Name} | {FormatNullable(localResult?.P95LatencyMs)} | {FormatDouble(localResult?.ThroughputPerSecond)} | {FormatDouble(localResult?.ErrorRatePercent)} | {verdict} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Target Check: Single Node");
        sb.AppendLine();
        sb.AppendLine("| Target | SQLite client |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Single key lookup p95 <= 80 ms | {DescribeTarget(sqliteClient, "medium-point-1-c1")} |");
        sb.AppendLine($"| 100-row query p95 <= 120 ms | {DescribeTarget(sqliteClient, "medium-range-100-c10")} |");
        sb.AppendLine($"| 1,000-row query p95 <= 750 ms | {DescribeTarget(sqliteClient, "medium-range-1000-c10")} |");
        sb.AppendLine($"| Error rate under load < 2% | {DescribeErrorTarget(sqliteClient.Values)} |");
        sb.AppendLine($"| No severe degradation at 50+ users | {DescribeConcurrencyTarget(sqliteClient, "medium-point-1-c50")} |");

        if (libsqlReplica.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Target Check: Distributed Replica");
            sb.AppendLine();
            sb.AppendLine("| Target | libSQL replica |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| Single key lookup p95 <= 80 ms | {DescribeTarget(libsqlReplica, "medium-point-1-c1")} |");
            sb.AppendLine($"| 100-row query p95 <= 120 ms | {DescribeTarget(libsqlReplica, "medium-range-100-c10")} |");
            sb.AppendLine($"| 1,000-row query p95 <= 750 ms | {DescribeTarget(libsqlReplica, "medium-range-1000-c10")} |");
            sb.AppendLine($"| Error rate under load < 2% | {DescribeErrorTarget(libsqlReplica.Values)} |");
            sb.AppendLine($"| No severe degradation at 50+ users | {DescribeConcurrencyTarget(libsqlReplica, "medium-point-1-c50")} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Bottlenecks");
        sb.AppendLine();
        sb.AppendLine("- `sqlite-client` includes Nona's database-client and row-mapping overhead on top of local SQLite storage.");
        if (libsqlReplica.Count > 0)
        {
            sb.AppendLine("- Replica freshness depends on embedded replica sync cadence. Cross-node reads can stay stale until next successful pull.");
            sb.AppendLine("- Direct primary path includes network round-trip on every read. Embedded replica path trades some freshness for local read latency.");
            sb.AppendLine("- Large dataset bootstrap and pull cost still scale with replicated data size and upstream bandwidth.");
        }

        sb.AppendLine();
        sb.AppendLine("## Recommendation");
        sb.AppendLine();

        if (severeLocalFailures.Count == 0)
        {
            sb.AppendLine("- Single-node mode can use `sqlite-client` as baseline storage path.");
        }
        else
        {
            sb.AppendLine("- Do not treat `sqlite-client` as single-node baseline until targets pass.");
        }

        if (libsqlReplica.Count > 0)
        {
            if (severeReplicaFailures.Count == 0)
            {
                sb.AppendLine("- Distributed mode also looks viable under tested replica load.");
            }
            else
            {
                sb.AppendLine("- Distributed embedded-replica mode still needs tuning before calling it baseline-safe.");
            }
        }

        return sb.ToString();
    }

    private static string DescribeTarget(
        IReadOnlyDictionary<string, BenchmarkResult> results,
        string scenarioName)
    {
        return results.TryGetValue(scenarioName, out var result)
            ? $"{(result.MeetsP95Target ? "pass" : "fail")} ({FormatNullable(result.P95LatencyMs)} ms)"
            : "missing";
    }

    private static string DescribeErrorTarget(IEnumerable<BenchmarkResult> results)
    {
        var maxError = results
            .Where(result => result.Required)
            .Select(result => result.ErrorRatePercent)
            .DefaultIfEmpty(0)
            .Max();

        return maxError < 2
            ? $"pass ({maxError:F2}%)"
            : $"fail ({maxError:F2}%)";
    }

    private static string DescribeConcurrencyTarget(
        IReadOnlyDictionary<string, BenchmarkResult> results,
        string scenarioName)
    {
        return results.TryGetValue(scenarioName, out var result)
            ? result.Timeouts == 0 && result.ErrorRatePercent < 2
                ? $"pass ({FormatNullable(result.P95LatencyMs)} ms, {result.ErrorRatePercent:F2}% err)"
                : $"fail ({FormatNullable(result.P95LatencyMs)} ms, {result.ErrorRatePercent:F2}% err)"
            : "missing";
    }

    private static void AppendCsvLine(StringBuilder sb, IEnumerable<string> values)
    {
        sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value}\""
            : value;
    }

    private static string FormatNullable(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("F3", CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static string FormatDouble(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("F3", CultureInfo.InvariantCulture)
            : "n/a";
    }
}
