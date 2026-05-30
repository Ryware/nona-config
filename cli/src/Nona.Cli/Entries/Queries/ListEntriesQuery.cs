namespace Nona.Cli.Entries.Queries;

internal sealed record ListEntriesQuery(NonaCliConnectionOptions Connection, string Project, string Environment);

internal sealed class ListEntriesQueryHandler
{
    private readonly Func<HttpClient> _createClient;

    internal ListEntriesQueryHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(ListEntriesQuery query, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(query.Connection.BaseUrl, query.Connection.BearerToken!, http);
        var entries = await api.ListConfigEntriesAsync(query.Project, query.Environment, ct);

        if (entries.Count == 0)
        {
            Console.WriteLine($"No config entries found in [{query.Environment}].");
            return 0;
        }

        Console.WriteLine($"Config entries — {query.Project} / {query.Environment}");
        foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
            WriteEntry(entry);

        return 0;
    }

    internal static void WriteEntry(ConfigEntryDto entry)
    {
        Console.WriteLine($"  {entry.Key}");
        Console.WriteLine($"    Value:        {entry.Value}");
        Console.WriteLine($"    Scope:        {entry.Scope}");
        Console.WriteLine($"    Content-Type: {entry.ContentType}");
        Console.WriteLine($"    Updated:      {entry.UpdatedAt:O}");
    }
}
