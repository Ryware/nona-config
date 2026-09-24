using Nona.Cli.Generated.Models;
using Nona.Cli.Entries;
using Nona.Cli.Releases;

namespace Nona.Cli.Entries.Queries;

internal sealed record ListEntriesQuery(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string? Prefix = null,
    bool UseReleases = false,
    string? ReleaseVersion = null);

internal sealed class ListEntriesQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(ListEntriesQuery query, CancellationToken ct)
    {
        using var api = NonaClientFactory.Create(query.Connection, httpClientFactory);

        if (query.UseReleases)
            return await HandleReleaseAsync(api, query, ct);

        var entries = await api.Admin.Projects[query.Project]
            .Environments[query.Environment].ConfigEntries.GetAsync(
                request => request.QueryParameters.Prefix = query.Prefix,
                cancellationToken: ct);

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

    private async Task<int> HandleReleaseAsync(OwnedNonaApiClient api, ListEntriesQuery query, CancellationToken ct)
    {
        if (query.ReleaseVersion is not null && !ReleaseVersions.TryParseExact(query.ReleaseVersion, out _))
        {
            Console.Error.WriteLine("--release-version must be an exact major.minor.patch version, for example 1.2.3.");
            return CliExitCodes.ValidationError;
        }

        var result = await AdminReleaseEntryReader.ReadAsync(api, query.Project, query.Environment, query.ReleaseVersion, ct);
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Error);
            return CliExitCodes.NotFound;
        }

        var entries = (result.Entries ?? [])
            .Where(entry => query.Prefix is null || (entry.Key?.StartsWith(query.Prefix, StringComparison.Ordinal) ?? false))
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (entries.Count == 0)
        {
            Console.WriteLine($"No config entries found in [{query.Environment}] release {result.ResolvedVersion}.");
            return CliExitCodes.Success;
        }

        Console.WriteLine($"Config entries — {query.Project} / {query.Environment} @ release {result.ResolvedVersion}");
        foreach (var entry in entries)
            WriteEntry(entry.ToConfigEntryDto(query.Project, query.Environment));

        return CliExitCodes.Success;
    }

    internal static void WriteEntry(ConfigEntryDto entry)
    {
        Console.WriteLine($"  {entry.Key}");
        Console.WriteLine($"    Value:        {entry.Value}");
        Console.WriteLine($"    Scope:        {entry.Scope}");
        Console.WriteLine($"    Content-Type: {ConfigEntryValueRenderer.NormalizeContentType(entry.ContentType)}");
        if (entry.UpdatedAt is not null)
            Console.WriteLine($"    Updated:      {entry.UpdatedAt:O}");
    }
}
