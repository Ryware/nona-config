using Nona.Cli.Generated.Models;
using Nona.Cli.Projects.Queries;

namespace Nona.Cli.Projects.Commands;

internal sealed record CreateProjectCommand(NonaCliConnectionOptions Connection, string Name);

internal sealed class CreateProjectCommandHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(CreateProjectCommand command, CancellationToken ct)
    {

        var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        var project = await api.Admin.Projects
            .PostAsync(new CreateProjectRequest { Name = command.Name }, cancellationToken: ct);

        Console.WriteLine($"Created project: {project!.Name}");
        ListProjectsQueryHandler.WriteProject(project);
        return 0;
    }
}
