using System.Diagnostics;

namespace Nona.Benchmarks;

internal static class LocalProcessResourceSampler
{
    public static double ReadCurrentRamMb(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Refresh();
        return process.WorkingSet64 / 1024d / 1024d;
    }

    public static async Task<ProcessMemorySummary> SampleUntilCancelledAsync(
        int processId,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        using var process = Process.GetProcessById(processId);
        var samples = new List<LocalProcessSample>();
        var previousCpu = process.TotalProcessorTime;
        var previousTimestamp = Stopwatch.GetTimestamp();
        var initialRamBytes = process.WorkingSet64;
        string? lastError = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken);
                process.Refresh();

                var timestamp = Stopwatch.GetTimestamp();
                var cpu = process.TotalProcessorTime;
                var elapsedSeconds = (timestamp - previousTimestamp) / (double)Stopwatch.Frequency;
                var cpuSeconds = (cpu - previousCpu).TotalSeconds;
                var cpuPercent = elapsedSeconds <= 0
                    ? 0
                    : cpuSeconds / elapsedSeconds / Environment.ProcessorCount * 100;

                samples.Add(new LocalProcessSample(cpuPercent, process.WorkingSet64));
                previousCpu = cpu;
                previousTimestamp = timestamp;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
            }
        }

        if (samples.Count == 0)
        {
            return ProcessMemorySummary.Unavailable(
                lastError is null ? "No local process resource samples were collected." :
                $"Local process resource sampling failed: {lastError}");
        }

        var cpuValues = samples.Select(sample => sample.CpuPercent).ToArray();
        var ramValues = samples.Select(sample => sample.RamBytes).ToArray();
        return new ProcessMemorySummary(
            samples.Count,
            cpuValues.Average(),
            cpuValues.Max(),
            initialRamBytes / 1024d / 1024d,
            ramValues.Average() / 1024d / 1024d,
            ramValues.Max() / 1024d / 1024d,
            null,
            null,
            lastError is null ? $"Local process {processId} sampled." : $"Last sampling error: {lastError}");
    }

    private sealed record LocalProcessSample(double CpuPercent, long RamBytes);
}
