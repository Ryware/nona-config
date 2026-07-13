using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries.Commands;

internal sealed record SetEntryCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Key,
    string Value,
    string? Scope,
    string? ContentType);

internal sealed class SetEntryCommandHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(SetEntryCommand command, CancellationToken ct)
    {

        var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        await api.Admin.Projects[command.Project]
            .Environments[command.Environment].ConfigEntries[command.Key]
            .PutAsync(new UpsertConfigEntryRequest
            {
                Value = command.Value,
                Scope = command.Scope,
                ContentType = command.ContentType
            }, cancellationToken: ct);

        Console.WriteLine($"Set [{command.Environment}] {command.Key}");
        return 0;
    }
}
