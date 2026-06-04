namespace Nona.Cli.Keys.Queries;

internal sealed record ShowKeysQuery(NonaCliConnectionOptions Connection, string Project);

internal sealed class ShowKeysQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    private readonly CliHttpJsonClient _client = new(httpClientFactory);

    public async Task<int> HandleAsync(ShowKeysQuery query, CancellationToken ct)
    {
        var result = await _client.SendAsync<IReadOnlyList<ApiKeyDto>>(
            query.Connection,
            HttpMethod.Get,
            $"admin/projects/{Segment(query.Project)}/api-keys",
            body: null,
            ct);

        if (!result.Success)
        {
            Console.Error.WriteLine(result.Error);
            return 1;
        }

        WriteKeys(query.Connection.BaseUrl, query.Project, result.Value ?? []);
        return 0;
    }

    internal static void WriteKeys(string baseUrl, string project, IReadOnlyList<ApiKeyDto> apiKeys)
    {
        Console.WriteLine("API keys");
        Console.WriteLine($"Base URL: {baseUrl}");
        Console.WriteLine($"Project:  {project}");

        if (apiKeys.Count == 0)
        {
            Console.WriteLine("No API keys found.");
            return;
        }

        foreach (var key in apiKeys.OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase))
        {
            WriteKey(key);
        }
    }

    internal static void WriteKey(ApiKeyDto key)
    {
        Console.WriteLine($"  {key.Id}: {key.Name}");
        Console.WriteLine($"    Key:         {key.Key}");
        Console.WriteLine($"    Environment: {key.Environment ?? "Project-wide"}");
        Console.WriteLine($"    Scope:       {key.Scope}");
        Console.WriteLine($"    Created:     {key.CreatedAt:O}");
    }

    private static string Segment(string value) => Uri.EscapeDataString(value);
}
