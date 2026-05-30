namespace Nona.Cli.Keys.Queries;

internal sealed record ShowKeysQuery(NonaCliConnectionOptions Connection, string Project);

internal sealed class ShowKeysQueryHandler
{
    private readonly Func<HttpClient> _createClient;

    internal ShowKeysQueryHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(ShowKeysQuery query, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(query.Connection.BaseUrl, query.Connection.BearerToken!, http);
        var project = await api.GetProjectAsync(query.Project, ct);

        if (project is null)
        {
            Console.Error.WriteLine($"Project '{query.Project}' was not found.");
            return 1;
        }

        WriteProject(query.Connection.BaseUrl, "Current keys", project);
        return 0;
    }

    internal static void WriteProject(string baseUrl, string title, ProjectDto project)
    {
        Console.WriteLine(title);
        Console.WriteLine($"Base URL:   {baseUrl}");
        Console.WriteLine($"Project:    {project.Name}");
        Console.WriteLine($"Slug:       {project.UrlSlug ?? "(none)"}");
        Console.WriteLine($"Server key: {project.ServerApiKey ?? "(none)"}");
        Console.WriteLine($"Client key: {project.ClientApiKey ?? "(none)"}");
        var environments = project.Environments.Count == 0
            ? "(none)"
            : string.Join(", ", project.Environments.OrderBy(static e => e, StringComparer.OrdinalIgnoreCase));
        Console.WriteLine($"Environments: {environments}");
    }
}
