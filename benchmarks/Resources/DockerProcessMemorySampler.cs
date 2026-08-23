using System.Diagnostics;
using System.Globalization;

namespace Nona.Benchmarks;

internal sealed record ProcessMemorySummary(
    int SampleCount,
    double? CSharpAverageCpuPercent,
    double? CSharpPeakCpuPercent,
    double? CSharpInitialRamMb,
    double? CSharpAverageRamMb,
    double? CSharpPeakRamMb,
    double? SqldAverageRamMb,
    double? SqldPeakRamMb,
    string? Note)
{
    public static ProcessMemorySummary Unavailable(string? note = null)
        => new(0, null, null, null, null, null, null, null, note);
}

internal static class DockerProcessMemorySampler
{
    public static async Task<ProcessMemorySummary> SampleUntilCancelledAsync(
        string containerName,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        var samples = new List<ProcessMemorySample>();
        string? lastError = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                samples.Add(await ReadSampleAsync(containerName, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
            }

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        if (samples.Count == 0)
        {
            return ProcessMemorySummary.Unavailable(
                lastError is null ? "No memory samples were collected." : $"Memory sampling failed: {lastError}");
        }

        var csharpValues = samples.Select(sample => sample.CSharpRssBytes).ToArray();
        var sqldValues = samples.Select(sample => sample.SqldRssBytes).ToArray();
        var matchedProcess = csharpValues.Any(value => value > 0) || sqldValues.Any(value => value > 0);
        var note = matchedProcess
            ? lastError is null ? null : $"Last sampling error: {lastError}"
            : "No Nona.WebApi or sqld process was found in docker top output.";

        return new ProcessMemorySummary(
            samples.Count,
            null,
            null,
            ToMebibytes(csharpValues[0]),
            ToMebibytes(csharpValues.Average()),
            ToMebibytes(csharpValues.Max()),
            ToMebibytes(sqldValues.Average()),
            ToMebibytes(sqldValues.Max()),
            note);
    }

    private static async Task<ProcessMemorySample> ReadSampleAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("top");
        process.StartInfo.ArgumentList.Add(containerName);
        process.StartInfo.ArgumentList.Add("-eo");
        process.StartInfo.ArgumentList.Add("pid,rss,comm,args");

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start docker top.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        using var commandSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        commandSource.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await process.WaitForExitAsync(commandSource.Token);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker top exited with code {process.ExitCode}: {error.Trim()}");
        }

        return ParseSample(output);
    }

    private static ProcessMemorySample ParseSample(string output)
    {
        long csharpRssBytes = 0;
        long sqldRssBytes = 0;

        foreach (var line in output.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var columns = line.Split(
                (char[]?)null,
                4,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (columns.Length < 3 || !TryParseRssBytes(columns[1], out var rssBytes))
            {
                continue;
            }

            var processIdentity = string.Join(' ', columns.Skip(2));
            if (processIdentity.Contains("sqld", StringComparison.OrdinalIgnoreCase))
            {
                sqldRssBytes += rssBytes;
            }
            else if (processIdentity.Contains("Nona.WebApi", StringComparison.OrdinalIgnoreCase) ||
                     processIdentity.Contains("Nona.WebApi.dll", StringComparison.OrdinalIgnoreCase))
            {
                csharpRssBytes += rssBytes;
            }
        }

        return new ProcessMemorySample(csharpRssBytes, sqldRssBytes);
    }

    private static bool TryParseRssBytes(string value, out long bytes)
    {
        bytes = 0;
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return false;
        }

        var multiplier = 1024d;
        if (!char.IsDigit(normalized[^1]))
        {
            multiplier = char.ToLowerInvariant(normalized[^1]) switch
            {
                'k' => 1024d,
                'm' => 1024d * 1024d,
                'g' => 1024d * 1024d * 1024d,
                _ => 0
            };
            normalized = normalized[..^1];
        }

        if (multiplier == 0 ||
            !double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) ||
            amount < 0)
        {
            return false;
        }

        bytes = checked((long)(amount * multiplier));
        return true;
    }

    private static double ToMebibytes(double bytes)
        => bytes / (1024d * 1024d);

    private sealed record ProcessMemorySample(long CSharpRssBytes, long SqldRssBytes);
}

