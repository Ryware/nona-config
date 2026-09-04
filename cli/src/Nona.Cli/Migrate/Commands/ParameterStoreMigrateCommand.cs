using Nona.Migrator.AwsParameterStore;

namespace Nona.Cli.Migrate.Commands;

internal sealed record ParameterStoreMigrateCommand(string[] Args);

internal sealed class ParameterStoreMigrateCommandHandler
{
    public Task<int> HandleAsync(ParameterStoreMigrateCommand command, CancellationToken cancellationToken)
        => AwsParameterStoreMigrationCommand.RunAsync(command.Args, cancellationToken);
}
