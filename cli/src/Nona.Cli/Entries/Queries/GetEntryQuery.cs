using Microsoft.Kiota.Abstractions;
using Nona.Cli.Entries.Queries;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries.Queries;

internal sealed record GetEntryQuery(NonaCliConnectionOptions Connection, string Project, string Environment, string Key);

internal sealed class GetEntryQueryHandler
{


    public async Task<int> HandleAsync(GetEntryQuery query, CancellationToken ct)
    {
        
        var api = NonaClientFactory.Create(query.Connection);

        ConfigEntryDto? entry;
        try
        {
            entry = await api.Admin.Projects[query.Project]
                .Environments[query.Environment].ConfigEntries[query.Key]
                .GetAsync(cancellationToken: ct);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 404)
        {
            Console.Error.WriteLine($"Entry '{query.Key}' not found in [{query.Environment}].");
            return 1;
        }

        ListEntriesQueryHandler.WriteEntry(entry!);
        return 0;
    }
}
