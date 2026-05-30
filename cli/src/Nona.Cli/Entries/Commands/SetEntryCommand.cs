namespace Nona.Cli.Entries.Commands;

internal sealed record SetEntryCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Key,
    string Value,
    string? Scope,
    string? ContentType);

internal sealed class SetEntryCommandHandler
{
    private readonly Func<HttpClient> _createClient;

    internal SetEntryCommandHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(SetEntryCommand command, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(command.Connection.BaseUrl, command.Connection.BearerToken!, http);
        await api.UpsertConfigEntryAsync(
            command.Project, command.Environment, command.Key,
            command.Value, command.Scope, command.ContentType, ct);

        Console.WriteLine($"Set [{command.Environment}] {command.Key}");
        return 0;
    }
}
