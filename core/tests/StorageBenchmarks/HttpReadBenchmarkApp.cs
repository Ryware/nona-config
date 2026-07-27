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

            var targets = new[]
            {
                new BenchmarkTarget("SQLite", options.SqliteUrl, options.SqliteContainer),
                new BenchmarkTarget("sqld", options.SqldUrl, options.SqldContainer)
            };
            var scenarios = CreateScenarios();
            var results = new List<HttpReadResult>();

            foreach (var target in targets)
            {
                using var client = CreateClient(target.BaseUrl);

                foreach (var keyCount in DatabaseSeeder.DatasetRows.Values)
                {
                    await ValidateDatasetAsync(client, keyCount, cancellationSource.Token);
                }

                foreach (var scenario in scenarios)
                {
                    Console.WriteLine(
                        $"Running {target.Provider} / {scenario.KeyCount:N0} keys / c{scenario.Concurrency}.");
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
            MaxConnectionsPerServer = MultiUserConcurrency,
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

    private static async Task ValidateDatasetAsync(
        HttpClient client,
        int expectedKeyCount,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            BuildRequestPath(expectedKeyCount),
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
                        BuildRequestPath(scenario.KeyCount),
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
                scenario.KeyCount,
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
            scenario.KeyCount,
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

    private static IReadOnlyList<HttpReadScenario> CreateScenarios()
    {
        return DatabaseSeeder.DatasetRows.Values
            .SelectMany(keyCount => new[]
            {
                new HttpReadScenario(keyCount, 1),
                new HttpReadScenario(keyCount, MultiUserConcurrency)
            })
            .ToArray();
    }

    private static string BuildRequestPath(int keyCount)
    {
        var dataset = DatabaseSeeder.DatasetRows.Single(pair => pair.Value == keyCount).Key;
        return $"/api/{DatabaseSeeder.GetEnvironmentName(dataset)}";
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
            "provider,key_count,concurrency,attempts,successes,failures,error_rate_percent,requests_per_second,average_latency_ms,p50_latency_ms,p95_latency_ms,p99_latency_ms,errors,memory_sample_count,csharp_average_ram_mb,csharp_peak_ram_mb,sqld_average_ram_mb,sqld_peak_ram_mb,memory_note");
        foreach (var result in results)
        {
            builder.AppendLine(string.Join(
                ",",
                new[]
                {
                    result.Provider,
                    result.KeyCount.ToString(CultureInfo.InvariantCulture),
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
        builder.AppendLine("- Full HTTP `GET /api/{environment}` requests against containerized Nona instances.");
        builder.AppendLine("- SQLite standalone compared with standalone managed sqld; no replica is involved.");
        builder.AppendLine("- Every 200 response body is fully consumed. No conditional ETag header is sent.");
        builder.AppendLine("- Latency percentiles include successful responses only; failures are reported separately.");
        builder.AppendLine($"- Single user is concurrency 1; multi-user load is concurrency {MultiUserConcurrency}.");
        builder.AppendLine($"- Warmup {summary.Options.WarmupDuration.TotalSeconds:F1}s; measurement {summary.Options.MeasurementDuration.TotalSeconds:F1}s per scenario.");
        if (summary.Results.Any(result => result.MemorySampleCount > 0))
        {
            builder.AppendLine($"- Process RAM is sampled from `docker top` every {summary.Options.MemorySampleInterval.TotalMilliseconds:F0} ms and reported as RSS.");
        }
        builder.AppendLine($"- Client OS: {summary.OsDescription}; logical cores: {summary.ProcessorCount}; .NET: {summary.DotnetVersion}.");
        builder.AppendLine();
        builder.AppendLine("## Results");
        builder.AppendLine();
        builder.AppendLine("| Provider | Keys returned | Concurrent users | p50 ms | p95 ms | p99 ms | req/s | Error % | C# RAM avg/peak MiB | sqld RAM avg/peak MiB | Errors |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var result in summary.Results)
        {
            builder.AppendLine(
                $"| {result.Provider} | {result.KeyCount:N0} | {result.Concurrency} | {result.P50LatencyMs:F2} | {result.P95LatencyMs:F2} | {result.P99LatencyMs:F2} | {result.RequestsPerSecond:F1} | {result.ErrorRatePercent:F2} | {FormatMemory(result.CSharpAverageRamMb, result.CSharpPeakRamMb)} | {FormatMemory(result.SqldAverageRamMb, result.SqldPeakRamMb)} | {FormatErrors(result.Errors)} |");
        }

        var memoryNotes = summary.Results
            .Where(result => !string.IsNullOrWhiteSpace(result.MemoryNote))
            .Select(result => $"{result.Provider}, {result.KeyCount:N0} keys, c{result.Concurrency}: {result.MemoryNote}")
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

        if (sqliteUrl is null || sqldUrl is null)
        {
            throw new ArgumentException("--sqlite-url and --sqld-url are required.");
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

    private sealed record HttpReadScenario(int KeyCount, int Concurrency);

    private sealed record HttpReadOptions(
        Uri SqliteUrl,
        Uri SqldUrl,
        string? SqliteContainer,
        string? SqldContainer,
        string OutputDirectory,
        TimeSpan WarmupDuration,
        TimeSpan MeasurementDuration,
        TimeSpan OperationTimeout,
        TimeSpan MemorySampleInterval);

    private sealed record HttpReadResult(
        string Provider,
        int KeyCount,
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
