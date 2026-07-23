namespace Nona.Cli.Releases.Queries;

internal sealed record ViewReleaseQuery(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Version);

internal sealed class ViewReleaseQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(ViewReleaseQuery query, CancellationToken cancellationToken)
    {
        using var api = NonaClientFactory.Create(query.Connection, httpClientFactory);
        var release = await api.Admin.Projects[query.Project]
            .Environments[query.Environment]
            .Releases[query.Version]
            .GetAsync(cancellationToken: cancellationToken);

        ReleaseRenderer.WriteDetails(release!);
        return 0;
    }
}
