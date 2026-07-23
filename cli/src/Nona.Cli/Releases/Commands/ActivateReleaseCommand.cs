using Nona.Cli.Generated.Models;

namespace Nona.Cli.Releases.Commands;

internal sealed record ActivateReleaseCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Version);

internal sealed class ActivateReleaseCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(ActivateReleaseCommand command, CancellationToken cancellationToken)
    {
        using var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        var environment = await api.Admin.Projects[command.Project]
            .Environments[command.Environment]
            .ActiveRelease
            .PutAsync(
                new SetActiveConfigReleaseRequest { Version = command.Version },
                cancellationToken: cancellationToken);

        Console.WriteLine(
            $"Active release for {command.Project} / {command.Environment}: " +
            environment!.ActiveReleaseVersion);
        return 0;
    }
}
