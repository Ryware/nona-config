namespace Nona.Cli.Releases.Commands;

internal sealed record ClearActiveReleaseCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment);

internal sealed class ClearActiveReleaseCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(
        ClearActiveReleaseCommand command,
        CancellationToken cancellationToken)
    {
        using var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        await api.Admin.Projects[command.Project]
            .Environments[command.Environment]
            .ActiveRelease
            .DeleteAsync(cancellationToken: cancellationToken);

        Console.WriteLine(
            $"Cleared active release for {command.Project} / {command.Environment}.");
        return 0;
    }
}
