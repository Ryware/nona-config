using Nona.Cli.Keys.Queries;

namespace Nona.Cli.Keys.Commands;

internal sealed record CreateApiKeyCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Name,
    string? Environment,
    string? Scope);

internal sealed class CreateApiKeyCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    private readonly CliHttpJsonClient _client = new(httpClientFactory);

    public async Task<int> HandleAsync(CreateApiKeyCommand command, CancellationToken ct)
    {
        var result = await _client.SendAsync<ApiKeyDto>(
            command.Connection,
            HttpMethod.Post,
            $"admin/projects/{Segment(command.Project)}/api-keys",
            new CreateApiKeyRequest
            {
                Name = command.Name,
                Environment = string.IsNullOrWhiteSpace(command.Environment) ? null : command.Environment,
                Scope = string.IsNullOrWhiteSpace(command.Scope) ? "client" : command.Scope
            },
            ct);

        if (!result.Success || result.Value is null)
        {
            Console.Error.WriteLine(result.Error ?? "Failed to create API key.");
            return 1;
        }

        Console.WriteLine("Created API key");
        ShowKeysQueryHandler.WriteKey(result.Value);
        return 0;
    }

    private static string Segment(string value) => Uri.EscapeDataString(value);
}
