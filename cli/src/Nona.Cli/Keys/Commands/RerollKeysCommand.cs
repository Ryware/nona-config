using Nona.Cli.Keys.Queries;

namespace Nona.Cli.Keys.Commands;

internal sealed record RerollKeysCommand(NonaCliConnectionOptions Connection, string Project, string KeyType);

internal sealed class RerollKeysCommandHandler
{
    private readonly Func<HttpClient> _createClient;

    internal RerollKeysCommandHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(RerollKeysCommand command, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(command.Connection.BaseUrl, command.Connection.BearerToken!, http);
        var project = await api.RerollApiKeysAsync(command.Project, command.KeyType, ct);

        ShowKeysQueryHandler.WriteProject(command.Connection.BaseUrl, $"Rerolled {command.KeyType} key(s)", project);
        return 0;
    }
}
