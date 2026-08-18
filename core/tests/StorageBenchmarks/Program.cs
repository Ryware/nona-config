using Nona.StorageBenchmarks;

if (args.Length > 0 && args[0].Equals("seed", StringComparison.OrdinalIgnoreCase))
{
    return await BenchmarkSeedApp.RunAsync(args[1..]);
}

if (args.Length > 0 && args[0].Equals("http-read", StringComparison.OrdinalIgnoreCase))
{
    return await HttpReadBenchmarkApp.RunAsync(args[1..]);
}

if (args.Length > 0 && args[0].Equals("replica", StringComparison.OrdinalIgnoreCase))
{
    return await ReplicaBenchmarkApp.RunAsync(args[1..]);
}

return await StorageBenchmarkApp.RunAsync(args);
