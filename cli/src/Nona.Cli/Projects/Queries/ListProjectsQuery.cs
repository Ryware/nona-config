using Nona.Cli.Generated.Models;

namespace Nona.Cli.Projects.Queries;

internal sealed record ListProjectsQuery(NonaCliConnectionOptions Connection);

internal sealed class ListProjectsQueryHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(ListProjectsQuery query, CancellationToken ct)
    {

        using var api = NonaClientFactory.Create(query.Connection, httpClientFactory);
        var projects = await api.Admin.Projects.GetAsync(cancellationToken: ct);

        if (projects is null || projects.Count == 0)
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
        var envs = p.Environments is { Count: > 0 }
            ? string.Join(", ", p.Environments.Order())
            : "(none)";
        Console.WriteLine($"    Environments: {envs}");
    }
}
