namespace Nona.Cli.Projects.Queries;

internal sealed record ListProjectsQuery(NonaCliConnectionOptions Connection);

internal sealed class ListProjectsQueryHandler
{
    private readonly Func<HttpClient> _createClient;

    internal ListProjectsQueryHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(ListProjectsQuery query, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(query.Connection.BaseUrl, query.Connection.BearerToken!, http);
        var projects = await api.ListProjectsAsync(ct);

        if (projects.Count == 0)
        {
            Console.WriteLine("No projects found.");
            return 0;
        }

        foreach (var p in projects)
            WriteProject(p);

        return 0;
    }

    internal static void WriteProject(ProjectDto p)
    {
        Console.WriteLine($"  {p.Name}");
        Console.WriteLine($"    Slug:       {p.UrlSlug ?? "(none)"}");
        Console.WriteLine($"    Server key: {p.ServerApiKey ?? "(none)"}");
        Console.WriteLine($"    Client key: {p.ClientApiKey ?? "(none)"}");
        var envs = p.Environments.Count == 0 ? "(none)" : string.Join(", ", p.Environments.Order());
        Console.WriteLine($"    Environments: {envs}");
    }
}
