using Nona.Migrator.FirebaseRemoteConfig;

namespace Nona.Cli.Migrate.Commands;

internal sealed record FirebaseMigrateCommand(string[] Args);

internal sealed class FirebaseMigrateCommandHandler
{
    public Task<int> HandleAsync(FirebaseMigrateCommand command, CancellationToken ct) =>
        FirebaseRemoteConfigMigrationCommand.RunAsync(command.Args, ct);
}
