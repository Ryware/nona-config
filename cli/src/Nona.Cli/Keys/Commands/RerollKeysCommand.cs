using Nona.Cli.Generated.Models;
using Nona.Cli.Keys.Queries;

namespace Nona.Cli.Keys.Commands;

internal sealed record RerollKeysCommand(NonaCliConnectionOptions Connection, string Project, string KeyType);

internal sealed class RerollKeysCommandHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(RerollKeysCommand command, CancellationToken ct)
    {
        
        var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        var project = await api.Admin.Projects[command.Project].RerollKeys
            .PostAsync(new RerollApiKeysRequest { KeyType = command.KeyType }, cancellationToken: ct);

        WriteLegacyProject(command.Connection.BaseUrl, $"Rerolled {command.KeyType} key(s)", project!);
        return 0;
    }

    private static void WriteLegacyProject(string baseUrl, string title, ProjectDto project)
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
