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
        var result = await _client.SendAsync<CreatedApiKeyDto>(
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

        WriteCreatedKey("Created", result.Value);
        return 0;
    }

    internal static void WriteCreatedKey(string action, CreatedApiKeyDto apiKey)
    {
        Console.WriteLine($"{action} API key {apiKey.Id}: {apiKey.Name}");
        Console.WriteLine("Warning: store this secret now; it cannot be recovered after this command exits.");
        Console.WriteLine($"Key: {apiKey.Key}");
        Console.WriteLine($"Environment: {apiKey.Environment ?? "Project-wide"}");
        Console.WriteLine($"Scope: {apiKey.Scope}");
    }

    private static string Segment(string value) => Uri.EscapeDataString(value);
}
