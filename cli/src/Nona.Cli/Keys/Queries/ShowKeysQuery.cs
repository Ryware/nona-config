using Nona.Cli.Generated.Models;

namespace Nona.Cli.Keys.Queries;

internal sealed record ShowKeysQuery(NonaCliConnectionOptions Connection, string Project);

internal sealed class ShowKeysQueryHandler
{


    public async Task<int> HandleAsync(ShowKeysQuery query, CancellationToken ct)
    {
        
        var api = NonaClientFactory.Create(query.Connection);
        var projects = await api.Admin.Projects.GetAsync(cancellationToken: ct);

        var project = projects?.FirstOrDefault(p =>
            string.Equals(p.Name, query.Project, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.UrlSlug, query.Project, StringComparison.OrdinalIgnoreCase));

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
        var environments = project.Environments?.Count == 0
            ? "(none)"
            : string.Join(", ", (project.Environments ?? []).OrderBy(static e => e, StringComparer.OrdinalIgnoreCase));
        Console.WriteLine($"Environments: {environments}");
    }
}
