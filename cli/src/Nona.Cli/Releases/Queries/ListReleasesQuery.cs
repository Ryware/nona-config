namespace Nona.Cli.Releases.Queries;

internal sealed record ListReleasesQuery(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    bool Json = false);

internal sealed class ListReleasesQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(ListReleasesQuery query, CancellationToken cancellationToken)
    {
        using var api = NonaClientFactory.Create(query.Connection, httpClientFactory);
        var releases = await api.Admin.Projects[query.Project]
            .Environments[query.Environment]
            .Releases
            .GetAsync(cancellationToken: cancellationToken);

        var orderedReleases = (releases ?? [])
            .OrderByDescending(release => ParseVersion(release.Version))
            .ToList();

        if (query.Json)
        {
            ReleaseRenderer.WriteJsonSummaries(orderedReleases);
            return CliExitCodes.Success;
        }

        if (orderedReleases.Count == 0)
        {
            Console.WriteLine(
                $"No releases found for {query.Project} / {query.Environment}.");
            return CliExitCodes.Success;
        }

        Console.WriteLine($"Releases — {query.Project} / {query.Environment}");
        foreach (var release in orderedReleases)
        {
            ReleaseRenderer.WriteSummary(release);
        }

        return CliExitCodes.Success;
    }

    private static ReleaseVersion ParseVersion(string? value)
        => ReleaseVersions.TryParseExact(value, out var version)
            ? version
            : default;
}
