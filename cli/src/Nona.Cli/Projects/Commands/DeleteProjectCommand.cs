namespace Nona.Cli.Projects.Commands;

internal sealed record DeleteProjectCommand(NonaCliConnectionOptions Connection, string Project);

internal sealed class DeleteProjectCommandHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(DeleteProjectCommand command, CancellationToken ct)
    {
        
        var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        await api.Admin.Projects[command.Project].DeleteAsync(cancellationToken: ct);

        Console.WriteLine($"Deleted project: {command.Project}");
        return 0;
    }
}
