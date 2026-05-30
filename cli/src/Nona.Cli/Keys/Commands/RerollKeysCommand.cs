using Nona.Cli.Generated.Models;
using Nona.Cli.Keys.Queries;

namespace Nona.Cli.Keys.Commands;

internal sealed record RerollKeysCommand(NonaCliConnectionOptions Connection, string Project, string KeyType);

internal sealed class RerollKeysCommandHandler
{


    public async Task<int> HandleAsync(RerollKeysCommand command, CancellationToken ct)
    {
        
        var api = NonaClientFactory.Create(command.Connection);
        var project = await api.Admin.Projects[command.Project].RerollKeys
            .PostAsync(new RerollApiKeysRequest { KeyType = command.KeyType }, cancellationToken: ct);

        ShowKeysQueryHandler.WriteProject(command.Connection.BaseUrl, $"Rerolled {command.KeyType} key(s)", project!);
        return 0;
    }
}
