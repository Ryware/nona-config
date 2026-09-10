using Nona.Benchmarks;

if (args.Length > 0 && args[0].Equals("seed", StringComparison.OrdinalIgnoreCase))
{
    return await BenchmarkSeedApp.RunAsync(args[1..]);
}

return await StorageBenchmarkApp.RunAsync(args);

