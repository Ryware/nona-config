using Nona.StorageBenchmarks;

if (args.Length > 0 && args[0].Equals("replica", StringComparison.OrdinalIgnoreCase))
{
    return await ReplicaBenchmarkApp.RunAsync(args[1..]);
}

return await StorageBenchmarkApp.RunAsync(args);
