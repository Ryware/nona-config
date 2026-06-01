namespace Nona.Cli.Keys.Commands;

internal sealed record DeleteApiKeyCommand(NonaCliConnectionOptions Connection, string Project, long ApiKeyId);

internal sealed class DeleteApiKeyCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    private readonly CliHttpJsonClient _client = new(httpClientFactory);

    public async Task<int> HandleAsync(DeleteApiKeyCommand command, CancellationToken ct)
    {
        var result = await _client.SendAsync<object>(
            command.Connection,
            HttpMethod.Delete,
            $"admin/projects/{Segment(command.Project)}/api-keys/{command.ApiKeyId}",
            body: null,
            ct);

        if (!result.Success)
        {
            Console.Error.WriteLine(result.Error ?? "Failed to delete API key.");
            return 1;
        }

        Console.WriteLine($"Deleted API key {command.ApiKeyId}.");
        return 0;
    }

    private static string Segment(string value) => Uri.EscapeDataString(value);
}
