using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries.Queries;

internal sealed record HistoryEntriesQuery(NonaCliConnectionOptions Connection, string Project, string Environment, string Key);

internal sealed class HistoryEntriesQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(HistoryEntriesQuery query, CancellationToken ct)
    {
        var api = NonaClientFactory.Create(query.Connection, httpClientFactory);
        var versions = await api.Admin.Projects[query.Project]
            .Environments[query.Environment].ConfigEntries[query.Key]
            .History.GetAsync(cancellationToken: ct);

        if (versions is null || versions.Count == 0)
        {
            Console.WriteLine($"No history found for [{query.Environment}] {query.Key}.");
            return 0;
        }

        Console.WriteLine($"History — {query.Project} / {query.Environment} / {query.Key}");
        foreach (var version in versions.OrderByDescending(v => v.Version ?? 0))
            WriteVersion(version);

        return 0;
    }

    private static void WriteVersion(ConfigEntryVersionDto version)
    {
        Console.WriteLine($"  v{version.Version}");
        Console.WriteLine($"    Date:         {version.CreatedAt:O}");
        Console.WriteLine($"    Actor:        {version.Actor}");
        Console.WriteLine($"    Value:        {version.Value}");
        Console.WriteLine($"    Scope:        {version.Scope}");
        Console.WriteLine($"    Content-Type: {ConfigEntryValueRenderer.NormalizeContentType(version.ContentType)}");
    }
}
