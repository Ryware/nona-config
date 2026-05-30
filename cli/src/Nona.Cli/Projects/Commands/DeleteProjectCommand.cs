namespace Nona.Cli.Projects.Commands;

internal sealed record DeleteProjectCommand(NonaCliConnectionOptions Connection, string Project);

internal sealed class DeleteProjectCommandHandler
{
    private readonly Func<HttpClient> _createClient;

    internal DeleteProjectCommandHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(DeleteProjectCommand command, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(command.Connection.BaseUrl, command.Connection.BearerToken!, http);
        await api.DeleteProjectAsync(command.Project, ct);

        Console.WriteLine($"Deleted project: {command.Project}");
        return 0;
    }
}
