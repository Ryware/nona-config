namespace Nona.Cli.Entries.Commands;

internal sealed record RevokeEntryShareLinkCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Key,
    long ShareLinkId);

internal sealed class RevokeEntryShareLinkCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(RevokeEntryShareLinkCommand command, CancellationToken ct)
    {
        var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        await api.Admin.Projects[command.Project]
            .Environments[command.Environment].ConfigEntries[command.Key]
            .ShareLinks[command.ShareLinkId]
            .DeleteAsync(cancellationToken: ct);

        Console.WriteLine($"Revoked share link {command.ShareLinkId} for [{command.Environment}] {command.Key}");
        return 0;
    }
}
