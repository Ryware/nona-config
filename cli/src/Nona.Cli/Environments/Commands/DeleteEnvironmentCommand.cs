namespace Nona.Cli.Environments.Commands;

internal sealed record DeleteEnvironmentCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Name);

internal sealed class DeleteEnvironmentCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(DeleteEnvironmentCommand command, CancellationToken ct)
    {
        var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        await api.Admin.Projects[command.Project].Environments[command.Name]
            .DeleteAsync(cancellationToken: ct);

        Console.WriteLine($"Deleted environment: {command.Name}");
        return 0;
    }
}
