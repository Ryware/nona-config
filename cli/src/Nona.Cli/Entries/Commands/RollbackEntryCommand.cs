using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries.Commands;

internal sealed record RollbackEntryCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Key,
    int Version);

internal sealed class RollbackEntryCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(RollbackEntryCommand command, CancellationToken ct)
    {
        using var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        var entry = await api.Admin.Projects[command.Project]
            .Environments[command.Environment].ConfigEntries[command.Key]
            .Rollback.PostAsync(new RollbackConfigEntryRequest
            {
                Version = command.Version
            }, cancellationToken: ct);

        Console.WriteLine($"Rolled back [{command.Environment}] {command.Key} to v{command.Version}");
        if (entry?.ActiveVersion is not null)
            Console.WriteLine($"Active version: v{entry.ActiveVersion}");

        return 0;
    }
}
