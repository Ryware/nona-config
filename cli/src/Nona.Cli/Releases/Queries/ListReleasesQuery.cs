namespace Nona.Cli.Releases.Queries;

internal sealed record ListReleasesQuery(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment);

internal sealed class ListReleasesQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(ListReleasesQuery query, CancellationToken cancellationToken)
    {
        using var api = NonaClientFactory.Create(query.Connection, httpClientFactory);
        var releases = await api.Admin.Projects[query.Project]
            .Environments[query.Environment]
            .Releases
            .GetAsync(cancellationToken: cancellationToken);

        if (releases is null || releases.Count == 0)
        {
            Console.WriteLine(
                $"No releases found for {query.Project} / {query.Environment}.");
            return 0;
        }

        Console.WriteLine($"Releases — {query.Project} / {query.Environment}");
        foreach (var release in releases
            .OrderByDescending(release => ParseVersion(release.Version)))
        {
            ReleaseRenderer.WriteSummary(release);
        }

        return 0;
    }

    private static ReleaseVersion ParseVersion(string? value)
        => ReleaseVersions.TryParse(value, out var version)
            ? version
            : default;
}
