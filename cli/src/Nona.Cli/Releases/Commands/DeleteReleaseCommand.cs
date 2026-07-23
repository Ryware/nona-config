namespace Nona.Cli.Releases.Commands;

internal sealed record DeleteReleaseCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Version);

internal sealed class DeleteReleaseCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(DeleteReleaseCommand command, CancellationToken cancellationToken)
    {
        using var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        await using var response = await api.Admin.Projects[command.Project]
            .Environments[command.Environment]
            .Releases[command.Version]
            .DeleteAsync(cancellationToken: cancellationToken);

        Console.WriteLine($"Deleted release: {command.Version}");
        return 0;
    }
}
