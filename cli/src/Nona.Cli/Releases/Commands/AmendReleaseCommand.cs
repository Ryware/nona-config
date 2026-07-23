using Microsoft.Kiota.Abstractions;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Releases.Commands;

internal sealed record AmendReleaseCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string SourceVersion,
    string TargetVersion);

internal sealed class AmendReleaseCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(AmendReleaseCommand command, CancellationToken ct)
    {
        using var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        var releases = api.Admin.Projects[command.Project]
            .Environments[command.Environment].Releases;
        var source = await releases[command.SourceVersion].GetAsync(cancellationToken: ct);

        if (source?.Entries is null)
        {
            throw new InvalidOperationException(
                $"Release {command.SourceVersion} did not return its entries and cannot be amended.");
        }

        ConfigReleaseDetailsDto? release;
        try
        {
            release = await releases.PostAsync(
                new PublishConfigReleaseRequest
                {
                    Version = command.TargetVersion,
                    MakeActive = false,
                    Entries = source.Entries
                },
                cancellationToken: ct);
        }
        catch (ApiException exception) when (exception.ResponseStatusCode is 400 or 422)
        {
            var error = CliExceptionHandler.Describe(exception);
            Console.Error.WriteLine(error.Message);
            return error.ExitCode;
        }

        Console.WriteLine(
            $"Published amended release {release?.Version ?? command.TargetVersion} " +
            $"from {command.SourceVersion}.");
        return CliExitCodes.Success;
    }
}
