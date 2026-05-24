using Nona.Migrator.FirebaseRemoteConfig;

namespace Nona.FirebaseRemoteConfigMigrator;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        return await FirebaseRemoteConfigMigrationCommand.RunAsync(args, cancellationTokenSource.Token);
    }
}
