using Nona.Cli.Entries.Queries;

namespace Nona.Cli.Entries.Queries;

internal sealed record GetEntryQuery(NonaCliConnectionOptions Connection, string Project, string Environment, string Key);

internal sealed class GetEntryQueryHandler
{
    private readonly Func<HttpClient> _createClient;

    internal GetEntryQueryHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(GetEntryQuery query, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(query.Connection.BaseUrl, query.Connection.BearerToken!, http);
        var entry = await api.GetConfigEntryAsync(query.Project, query.Environment, query.Key, ct);

        if (entry is null)
        {
            Console.Error.WriteLine($"Entry '{query.Key}' not found in [{query.Environment}].");
            return 1;
        }

        ListEntriesQueryHandler.WriteEntry(entry);
        return 0;
    }
}
