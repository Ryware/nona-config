using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nona.StorageBenchmarks;

internal static class HttpReadBenchmarkApp
{
    private const int MultiUserConcurrency = 50;
    private const int HighConcurrency = 100;
    private const int SingleKeyIndex = 1;

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            var outputDirectory = Path.GetFullPath(options.OutputDirectory);
            Directory.CreateDirectory(outputDirectory);

            using var cancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationSource.Cancel();
            };

            var targets = CreateTargets(options);
            var scenarios = CreateScenarios(options.Operation);
            var results = new List<HttpReadResult>();

            foreach (var target in targets)
            {
                using var client = CreateClient(target.BaseUrl);

                if (scenarios.Any(scenario => scenario.Operation == HttpReadOperation.FullEnvironment))
                {
                    foreach (var keyCount in DatabaseSeeder.DatasetRows.Values)
                    {
                        await ValidateDatasetAsync(client, keyCount, cancellationSource.Token);
                    }
                }
                if (scenarios.Any(scenario => scenario.Operation == HttpReadOperation.SingleKey))
                {
                    await ValidateSingleKeyAsync(client, cancellationSource.Token);
                }

                foreach (var scenario in scenarios)
                {
                    Console.WriteLine(
                        $"Running {target.Provider} / {FormatOperation(scenario.Operation)} / " +
                        $"{scenario.DatasetKeyCount:N0} dataset keys / c{scenario.Concurrency}.");
                    await RunPhaseAsync(
                        client,
                        scenario,
                        options.WarmupDuration,
                        options.OperationTimeout,
                        measure: false,
                        cancellationSource.Token);

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    results.Add(await RunPhaseAsync(
                        client,
                        scenario,
                        options.MeasurementDuration,
                        options.OperationTimeout,
                        measure: true,
                        cancellationSource.Token,
                        target.Provider,
                        target.ContainerName,
                        options.MemorySampleInterval));
                }
            }

            var summary = new HttpReadSummary(
                DateTime.UtcNow,
                Environment.MachineName,
                RuntimeInformation.OSDescription,
                Environment.ProcessorCount,
                Environment.Version.ToString(),
                options,
                results);
            await WriteReportsAsync(outputDirectory, summary, cancellationSource.Token);

            Console.WriteLine($"Artifacts written to {outputDirectory}.");
            return results.Any(result => result.Failures > 0) ? 1 : 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("HTTP read benchmark cancelled.");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static HttpClient CreateClient(Uri baseUrl)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = HighConcurrency,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            UseCookies = false
        };
        var client = new HttpClient(handler)
        {
            BaseAddress = baseUrl,
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.Add("X-Api-Key", DatabaseSeeder.ApiKey);
        return client;
    }

    private static IReadOnlyList<BenchmarkTarget> CreateTargets(HttpReadOptions options)
    {
        var targets = new List<BenchmarkTarget>();
        if (options.SqliteUrl is not null)
        {
            targets.Add(new BenchmarkTarget("SQLite", options.SqliteUrl, options.SqliteContainer));
        }
        if (options.SqldUrl is not null)
        {
            targets.Add(new BenchmarkTarget("sqld", options.SqldUrl, options.SqldContainer));
        }

        return targets;
    }

    private static async Task ValidateDatasetAsync(
        HttpClient client,
        int expectedKeyCount,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            BuildFullEnvironmentRequestPath(expectedKeyCount),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var actualKeyCount = document.RootElement.EnumerateObject().Count();
        if (actualKeyCount != expectedKeyCount)
        {
            throw new InvalidOperationException(
                $"{client.BaseAddress} returned {actualKeyCount:N0} keys for the {expectedKeyCount:N0}-key dataset.");
        }
    }

    private static async Task ValidateSingleKeyAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var datasetKeyCount = DatabaseSeeder.DatasetRows[DatasetSize.Large];
        using var response = await client.GetAsync(
            BuildSingleKeyRequestPath(datasetKeyCount),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var actualValue = await response.Content.ReadAsStringAsync(cancellationToken);
        var expectedValue = DatabaseSeeder.BuildValue(
            DatabaseSeeder.GetEnvironmentName(DatasetSize.Large),
            SingleKeyIndex);
        if (!string.Equals(actualValue, expectedValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{client.BaseAddress} returned an unexpected value for the single-key benchmark.");
        }
    }

    private static async Task<HttpReadResult> RunPhaseAsync(
        HttpClient client,
        HttpReadScenario scenario,
        TimeSpan duration,
        TimeSpan operationTimeout,
        bool measure,
        CancellationToken cancellationToken,
        string provider = "",
        string? containerName = null,
        TimeSpan? memorySampleInterval = null)
    {
        var stopAt = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
        var latencies = new ConcurrentBag<double>();
        var errors = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var attempts = 0;
        var successes = 0;
        var failures = 0;
        var phaseStarted = Stopwatch.GetTimestamp();
        using var memorySource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var memoryTask = measure && !string.IsNullOrWhiteSpace(containerName)
            ? DockerProcessMemorySampler.SampleUntilCancelledAsync(
                containerName,
                memorySampleInterval ?? TimeSpan.FromMilliseconds(500),
                memorySource.Token)
            : Task.FromResult(ProcessMemorySummary.Unavailable());

        async Task WorkerAsync()
        {
            while (Stopwatch.GetTimestamp() < stopAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref attempts);
                using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                operationSource.CancelAfter(operationTimeout);
                var started = Stopwatch.GetTimestamp();

                try
                {
                    using var response = await client.GetAsync(
                        BuildRequestPath(scenario),
                        HttpCompletionOption.ResponseHeadersRead,
                        operationSource.Token);
                    response.EnsureSuccessStatusCode();

                    await using var stream = await response.Content.ReadAsStreamAsync(operationSource.Token);
                    await stream.CopyToAsync(Stream.Null, operationSource.Token);
                    Interlocked.Increment(ref successes);

                    if (measure)
                    {
                        latencies.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    }
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref failures);
                    if (measure)
                    {
                        errors.AddOrUpdate(DescribeError(exception), 1, (_, count) => count + 1);
                    }
                }
            }
        }

        await Task.WhenAll(
            Enumerable.Range(0, scenario.Concurrency).Select(_ => WorkerAsync()));
        var elapsed = Stopwatch.GetElapsedTime(phaseStarted);
        memorySource.Cancel();
        var memory = await memoryTask;

        if (!measure)
        {
            return new HttpReadResult(
                provider,
                scenario.Operation,
                scenario.DatasetKeyCount,
                scenario.KeysReturned,
                scenario.Concurrency,
                attempts,
                successes,
                failures,
                0,
                0,
                0,
                0,
                0,
                0,
                new Dictionary<string, int>(),
                memory.SampleCount,
                memory.CSharpAverageRamMb,
                memory.CSharpPeakRamMb,
                memory.SqldAverageRamMb,
                memory.SqldPeakRamMb,
                memory.Note);
        }

        var orderedLatencies = latencies.OrderBy(value => value).ToArray();
        return new HttpReadResult(
            provider,
            scenario.Operation,
            scenario.DatasetKeyCount,
            scenario.KeysReturned,
            scenario.Concurrency,
            attempts,
            successes,
            failures,
            attempts == 0 ? 0 : failures * 100d / attempts,
            successes / Math.Max(elapsed.TotalSeconds, 0.001),
            orderedLatencies.Length == 0 ? 0 : orderedLatencies.Average(),
            Percentile(orderedLatencies, 0.50),
            Percentile(orderedLatencies, 0.95),
            Percentile(orderedLatencies, 0.99),
            errors.OrderByDescending(pair => pair.Value)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            memory.SampleCount,
            memory.CSharpAverageRamMb,
            memory.CSharpPeakRamMb,
            memory.SqldAverageRamMb,
            memory.SqldPeakRamMb,
            memory.Note);
    }

    private static string DescribeError(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "timeout",
            HttpRequestException { StatusCode: { } statusCode } =>
                $"HTTP {(int)statusCode} {statusCode}",
            _ => $"{exception.GetType().Name}: {exception.Message}"
        };
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
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

    private static IReadOnlyList<HttpReadScenario> CreateScenarios(HttpReadOperation? operation)
    {
        var scenarios = new List<HttpReadScenario>();
        if (operation is null or HttpReadOperation.FullEnvironment)
        {
            scenarios.AddRange(DatabaseSeeder.DatasetRows.Values
                .SelectMany(keyCount => new[]
                {
                    new HttpReadScenario(HttpReadOperation.FullEnvironment, keyCount, keyCount, 1),
                    new HttpReadScenario(
                        HttpReadOperation.FullEnvironment,
                        keyCount,
                        keyCount,
                        MultiUserConcurrency)
                }));
        }

        if (operation is null or HttpReadOperation.SingleKey)
        {
            var singleKeyDatasetSize = DatabaseSeeder.DatasetRows[DatasetSize.Large];
            scenarios.AddRange(new[] { 1, MultiUserConcurrency, HighConcurrency }
                .Select(concurrency => new HttpReadScenario(
                    HttpReadOperation.SingleKey,
                    singleKeyDatasetSize,
                    1,
                    concurrency)));
        }

        return scenarios;
    }

    private static string BuildRequestPath(HttpReadScenario scenario)
    {
        return scenario.Operation switch
        {
            HttpReadOperation.FullEnvironment =>
                BuildFullEnvironmentRequestPath(scenario.DatasetKeyCount),
            HttpReadOperation.SingleKey =>
                BuildSingleKeyRequestPath(scenario.DatasetKeyCount),
            _ => throw new ArgumentOutOfRangeException(
                nameof(scenario),
                scenario.Operation,
                null)
        };
    }

    private static string BuildFullEnvironmentRequestPath(int datasetKeyCount)
    {
        return $"/api/{GetEnvironmentName(datasetKeyCount)}";
    }

    private static string BuildSingleKeyRequestPath(int datasetKeyCount)
    {
        return $"/api/{GetEnvironmentName(datasetKeyCount)}/" +
               Uri.EscapeDataString(DatabaseSeeder.BuildKey(SingleKeyIndex));
    }

    private static string GetEnvironmentName(int datasetKeyCount)
    {
        var dataset = DatabaseSeeder.DatasetRows.Single(pair => pair.Value == datasetKeyCount).Key;
        return Uri.EscapeDataString(DatabaseSeeder.GetEnvironmentName(dataset));
    }

    private static async Task WriteReportsAsync(
        string outputDirectory,
        HttpReadSummary summary,
        CancellationToken cancellationToken)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "results.json"),
            JsonSerializer.Serialize(summary, jsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "results.csv"),
            BuildCsv(summary.Results),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "REPORT.md"),
            BuildMarkdown(summary),
            cancellationToken);
    }

    private static string BuildCsv(IEnumerable<HttpReadResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "provider,operation,dataset_key_count,keys_returned,concurrency,attempts,successes,failures,error_rate_percent,requests_per_second,average_latency_ms,p50_latency_ms,p95_latency_ms,p99_latency_ms,errors,memory_sample_count,csharp_average_ram_mb,csharp_peak_ram_mb,sqld_average_ram_mb,sqld_peak_ram_mb,memory_note");
        foreach (var result in results)
        {
            builder.AppendLine(string.Join(
                ",",
                new[]
                {
                    result.Provider,
                    result.Operation.ToString(),
                    result.DatasetKeyCount.ToString(CultureInfo.InvariantCulture),
                    result.KeysReturned.ToString(CultureInfo.InvariantCulture),
                    result.Concurrency.ToString(CultureInfo.InvariantCulture),
                    result.Attempts.ToString(CultureInfo.InvariantCulture),
                    result.Successes.ToString(CultureInfo.InvariantCulture),
                    result.Failures.ToString(CultureInfo.InvariantCulture),
                    result.ErrorRatePercent.ToString("F3", CultureInfo.InvariantCulture),
                    result.RequestsPerSecond.ToString("F3", CultureInfo.InvariantCulture),
                    result.AverageLatencyMs.ToString("F3", CultureInfo.InvariantCulture),
                    result.P50LatencyMs.ToString("F3", CultureInfo.InvariantCulture),
                    result.P95LatencyMs.ToString("F3", CultureInfo.InvariantCulture),
                    result.P99LatencyMs.ToString("F3", CultureInfo.InvariantCulture),
                    string.Join(
                        "; ",
                        result.Errors.Select(pair => $"{pair.Key}={pair.Value}")),
                    result.MemorySampleCount.ToString(CultureInfo.InvariantCulture),
                    FormatNullable(result.CSharpAverageRamMb),
                    FormatNullable(result.CSharpPeakRamMb),
                    FormatNullable(result.SqldAverageRamMb),
                    FormatNullable(result.SqldPeakRamMb),
                    result.MemoryNote ?? string.Empty
                }.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string BuildMarkdown(HttpReadSummary summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Container HTTP Read Benchmark");
        builder.AppendLine();
        builder.AppendLine($"Generated: {summary.GeneratedAtUtc:O}");
        builder.AppendLine();
        builder.AppendLine("## Method");
        builder.AppendLine();
        if (summary.Results.Any(result => result.Operation == HttpReadOperation.FullEnvironment))
        {
            builder.AppendLine("- Full-environment reads use `GET /api/{environment}`.");
        }
        if (summary.Results.Any(result => result.Operation == HttpReadOperation.SingleKey))
        {
            builder.AppendLine("- Single-key reads use `GET /api/{environment}/{key}` for one fixed key in the 10,000-key dataset at concurrency 1, 50, and 100.");
        }
        var providers = summary.Results
            .Select(result => result.Provider)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        builder.AppendLine(providers.Length == 1
            ? $"- Provider: {providers[0]}."
            : "- SQLite standalone compared with standalone managed sqld; no replica is involved.");
        builder.AppendLine("- Every 200 response body is fully consumed. No conditional ETag header is sent.");
        builder.AppendLine("- Latency percentiles include successful responses only; failures are reported separately.");
        builder.AppendLine("- Concurrency is the number of closed-loop HTTP clients.");
        builder.AppendLine($"- Warmup {summary.Options.WarmupDuration.TotalSeconds:F1}s; measurement {summary.Options.MeasurementDuration.TotalSeconds:F1}s per scenario.");
        if (summary.Results.Any(result => result.MemorySampleCount > 0))
        {
            builder.AppendLine($"- Process RAM is sampled from `docker top` every {summary.Options.MemorySampleInterval.TotalMilliseconds:F0} ms and reported as RSS.");
        }
        builder.AppendLine($"- Client OS: {summary.OsDescription}; logical cores: {summary.ProcessorCount}; .NET: {summary.DotnetVersion}.");
        builder.AppendLine();
        builder.AppendLine("## Results");
        builder.AppendLine();
        builder.AppendLine("| Provider | Operation | Dataset keys | Keys returned | Concurrency | p50 ms | p95 ms | p99 ms | req/s | Error % | C# RAM avg/peak MiB | sqld RAM avg/peak MiB | Errors |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var result in summary.Results)
        {
            builder.AppendLine(
                $"| {result.Provider} | {FormatOperation(result.Operation)} | {result.DatasetKeyCount:N0} | {result.KeysReturned:N0} | {result.Concurrency} | {result.P50LatencyMs:F2} | {result.P95LatencyMs:F2} | {result.P99LatencyMs:F2} | {result.RequestsPerSecond:F1} | {result.ErrorRatePercent:F2} | {FormatMemory(result.CSharpAverageRamMb, result.CSharpPeakRamMb)} | {FormatMemory(result.SqldAverageRamMb, result.SqldPeakRamMb)} | {FormatErrors(result.Errors)} |");
        }

        var memoryNotes = summary.Results
            .Where(result => !string.IsNullOrWhiteSpace(result.MemoryNote))
            .Select(result => $"{result.Provider}, {FormatOperation(result.Operation)}, {result.DatasetKeyCount:N0} dataset keys, c{result.Concurrency}: {result.MemoryNote}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (memoryNotes.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Memory Sampling Notes");
            builder.AppendLine();
            foreach (var note in memoryNotes)
            {
                builder.AppendLine($"- {note}");
            }
        }

        return builder.ToString();
    }

    private static string FormatOperation(HttpReadOperation operation)
    {
        return operation switch
        {
            HttpReadOperation.FullEnvironment => "full environment",
            HttpReadOperation.SingleKey => "single key",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private static string FormatErrors(IReadOnlyDictionary<string, int> errors)
    {
        return errors.Count == 0
            ? "none"
            : string.Join("<br>", errors.Select(pair => $"{pair.Key}: {pair.Value}"));
    }

    private static string FormatMemory(double? averageRamMb, double? peakRamMb)
    {
        return averageRamMb.HasValue && peakRamMb.HasValue
            ? $"{averageRamMb.Value:F1} / {peakRamMb.Value:F1}"
            : "n/a";
    }

    private static string FormatNullable(double? value)
        => value?.ToString("F3", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string EscapeCsv(string value)
        => value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"")}\"";

    private static HttpReadOptions ParseOptions(string[] args)
    {
        Uri? sqliteUrl = null;
        Uri? sqldUrl = null;
        string? sqliteContainer = null;
        string? sqldContainer = null;
        string? outputDirectory = null;
        HttpReadOperation? operation = null;
        var warmupSeconds = 2d;
        var measurementSeconds = 8d;
        var timeoutSeconds = 30d;
        var memorySampleMilliseconds = 500d;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--sqlite-url":
                    sqliteUrl = ParseUrl(ReadValue(args, ref index));
                    break;
                case "--sqld-url":
                    sqldUrl = ParseUrl(ReadValue(args, ref index));
                    break;
                case "--sqlite-container":
                    sqliteContainer = ReadValue(args, ref index);
                    break;
                case "--sqld-container":
                    sqldContainer = ReadValue(args, ref index);
                    break;
                case "--output":
                    outputDirectory = ReadValue(args, ref index);
                    break;
                case "--operation":
                    operation = ParseOperation(ReadValue(args, ref index));
                    break;
                case "--warmup-seconds":
                    warmupSeconds = ParsePositiveDouble(ReadValue(args, ref index), "--warmup-seconds");
                    break;
                case "--measurement-seconds":
                    measurementSeconds = ParsePositiveDouble(ReadValue(args, ref index), "--measurement-seconds");
                    break;
                case "--timeout-seconds":
                    timeoutSeconds = ParsePositiveDouble(ReadValue(args, ref index), "--timeout-seconds");
                    break;
                case "--memory-sample-ms":
                    memorySampleMilliseconds = ParsePositiveDouble(ReadValue(args, ref index), "--memory-sample-ms");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'.");
            }
        }

        if (sqliteUrl is null && sqldUrl is null)
        {
            throw new ArgumentException("At least one of --sqlite-url or --sqld-url is required.");
        }

        outputDirectory ??= Path.Combine(
            ResolveRepoRoot(),
            "artifacts",
            "benchmarks",
            $"http-read-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
        return new HttpReadOptions(
            sqliteUrl,
            sqldUrl,
            sqliteContainer,
            sqldContainer,
            operation,
            outputDirectory,
            TimeSpan.FromSeconds(warmupSeconds),
            TimeSpan.FromSeconds(measurementSeconds),
            TimeSpan.FromSeconds(timeoutSeconds),
            TimeSpan.FromMilliseconds(memorySampleMilliseconds));
    }

    private static Uri ParseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException($"'{value}' is not an absolute HTTP URL.");
        }

        return uri;
    }

    private static double ParsePositiveDouble(string value, string option)
    {
        var parsed = double.Parse(value, CultureInfo.InvariantCulture);
        if (parsed <= 0)
        {
            throw new ArgumentOutOfRangeException(option, "Value must be greater than zero.");
        }

        return parsed;
    }

    private static HttpReadOperation? ParseOperation(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "all" => null,
            "full-environment" => HttpReadOperation.FullEnvironment,
            "single-key" => HttpReadOperation.SingleKey,
            _ => throw new ArgumentException(
                $"Unknown HTTP read operation '{value}'. Expected all, full-environment, or single-key.")
        };
    }

    private static string ReadValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException($"Missing value for '{args[index - 1]}'.");
        }

        return args[index];
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

        throw new DirectoryNotFoundException(
            "Could not find repository root containing NonaConfig.slnx.");
    }

    private sealed record BenchmarkTarget(
        string Provider,
        Uri BaseUrl,
        string? ContainerName);

    private enum HttpReadOperation
    {
        FullEnvironment,
        SingleKey
    }

    private sealed record HttpReadScenario(
        HttpReadOperation Operation,
        int DatasetKeyCount,
        int KeysReturned,
        int Concurrency);

    private sealed record HttpReadOptions(
        Uri? SqliteUrl,
        Uri? SqldUrl,
        string? SqliteContainer,
        string? SqldContainer,
        HttpReadOperation? Operation,
        string OutputDirectory,
        TimeSpan WarmupDuration,
        TimeSpan MeasurementDuration,
        TimeSpan OperationTimeout,
        TimeSpan MemorySampleInterval);

    private sealed record HttpReadResult(
        string Provider,
        HttpReadOperation Operation,
        int DatasetKeyCount,
        int KeysReturned,
        int Concurrency,
        int Attempts,
        int Successes,
        int Failures,
        double ErrorRatePercent,
        double RequestsPerSecond,
        double AverageLatencyMs,
        double P50LatencyMs,
        double P95LatencyMs,
        double P99LatencyMs,
        IReadOnlyDictionary<string, int> Errors,
        int MemorySampleCount,
        double? CSharpAverageRamMb,
        double? CSharpPeakRamMb,
        double? SqldAverageRamMb,
        double? SqldPeakRamMb,
        string? MemoryNote);

    private sealed record HttpReadSummary(
        DateTime GeneratedAtUtc,
        string MachineName,
        string OsDescription,
        int ProcessorCount,
        string DotnetVersion,
        HttpReadOptions Options,
        IReadOnlyList<HttpReadResult> Results);
}
