using Nona.Cli.Generated.Models;
using Nona.Cli.Entries;

namespace Nona.Cli.Entries.Queries;

internal sealed record ListEntriesQuery(NonaCliConnectionOptions Connection, string Project, string Environment);

internal sealed class ListEntriesQueryHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(ListEntriesQuery query, CancellationToken ct)
    {

        var api = NonaClientFactory.Create(query.Connection, httpClientFactory);
        var entries = await api.Admin.Projects[query.Project]
            .Environments[query.Environment].ConfigEntries.GetAsync(cancellationToken: ct);

        if (entries is null || entries.Count == 0)
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
        Console.WriteLine($"    Content-Type: {ConfigEntryValueRenderer.NormalizeContentType(entry.ContentType)}");
        Console.WriteLine($"    Updated:      {entry.UpdatedAt:O}");
    }
}
