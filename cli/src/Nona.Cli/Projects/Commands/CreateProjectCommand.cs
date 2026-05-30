using Nona.Cli.Projects.Queries;

namespace Nona.Cli.Projects.Commands;

internal sealed record CreateProjectCommand(NonaCliConnectionOptions Connection, string Name);

internal sealed class CreateProjectCommandHandler
{
    private readonly Func<HttpClient> _createClient;

    internal CreateProjectCommandHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(CreateProjectCommand command, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(command.Connection.BaseUrl, command.Connection.BearerToken!, http);
        var project = await api.CreateProjectAsync(command.Name, ct);

        Console.WriteLine($"Created project: {project.Name}");
        ListProjectsQueryHandler.WriteProject(project);
        return 0;
    }
}
