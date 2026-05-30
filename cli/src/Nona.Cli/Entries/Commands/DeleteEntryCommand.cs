namespace Nona.Cli.Entries.Commands;

internal sealed record DeleteEntryCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Key);

internal sealed class DeleteEntryCommandHandler
{
    private readonly Func<HttpClient> _createClient;

    internal DeleteEntryCommandHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(DeleteEntryCommand command, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(command.Connection.BaseUrl, command.Connection.BearerToken!, http);
        await api.DeleteConfigEntryAsync(command.Project, command.Environment, command.Key, ct);

        Console.WriteLine($"Deleted [{command.Environment}] {command.Key}");
        return 0;
    }
}
