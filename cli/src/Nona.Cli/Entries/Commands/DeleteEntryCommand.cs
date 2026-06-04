namespace Nona.Cli.Entries.Commands;

internal sealed record DeleteEntryCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Key);

internal sealed class DeleteEntryCommandHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(DeleteEntryCommand command, CancellationToken ct)
    {
        
        var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        await api.Admin.Projects[command.Project]
            .Environments[command.Environment].ConfigEntries[command.Key]
            .DeleteAsync(cancellationToken: ct);

        Console.WriteLine($"Deleted [{command.Environment}] {command.Key}");
        return 0;
    }
}
