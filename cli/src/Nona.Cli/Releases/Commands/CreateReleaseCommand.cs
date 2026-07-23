using Nona.Cli.Generated.Models;

namespace Nona.Cli.Releases.Commands;

internal sealed record CreateReleaseCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Version,
    bool Activate);

internal sealed class CreateReleaseCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(CreateReleaseCommand command, CancellationToken cancellationToken)
    {
        using var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        var release = await api.Admin.Projects[command.Project]
            .Environments[command.Environment]
            .Releases
            .PostAsync(
                new PublishConfigReleaseRequest
                {
                    Version = command.Version,
                    MakeActive = command.Activate
                },
                cancellationToken: cancellationToken);

        Console.WriteLine($"Created release: {release!.Version}");
        ReleaseRenderer.WriteDetails(release);
        return 0;
    }
}
