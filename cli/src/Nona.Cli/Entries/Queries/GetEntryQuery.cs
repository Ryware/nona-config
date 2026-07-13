using Microsoft.Kiota.Abstractions;
using Nona.Cli.Entries;
using Nona.Cli.Generated.Models;
using System.Net;

namespace Nona.Cli.Entries.Queries;

internal sealed record GetEntryQuery(NonaCliConnectionOptions Connection, string Project, string Environment, string Key);

internal sealed class GetEntryQueryHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(GetEntryQuery query, CancellationToken ct)
    {
        if (IsLikelyApiKey(query.Connection.BearerToken))
            return await GetRawEntryAsync(query, ct);

        return await GetAdminEntryAsync(query, ct);
    }

    private async Task<int> GetRawEntryAsync(GetEntryQuery query, CancellationToken ct)
    {
        using var http = httpClientFactory?.Invoke() ?? new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildRawEntryUrl(query.Connection.BaseUrl, query.Environment, query.Key));

        request.Headers.TryAddWithoutValidation("X-Api-Key", query.Connection.BearerToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Console.Error.WriteLine($"Entry '{query.Key}' not found in [{query.Environment}].");
            return 1;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Console.Error.WriteLine("API key is missing or invalid.");
            return 1;
        }

        response.EnsureSuccessStatusCode();

        var value = await response.Content.ReadAsStringAsync(ct);
        var contentType = ReadLogicalContentType(response);
        ConfigEntryValueRenderer.WriteValue(value, contentType);
        return 0;
    }

    private async Task<int> GetAdminEntryAsync(GetEntryQuery query, CancellationToken ct)
    {
        var api = NonaClientFactory.Create(query.Connection, httpClientFactory);

        ConfigEntryDto? entry;
        try
        {
            entry = await api.Admin.Projects[query.Project]
                .Environments[query.Environment].ConfigEntries[query.Key]
                .GetAsync(cancellationToken: ct);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 404)
        {
            Console.Error.WriteLine($"Entry '{query.Key}' not found in [{query.Environment}].");
            return 1;
        }

        ConfigEntryValueRenderer.WriteValue(entry!.Value ?? string.Empty, entry.ContentType);
        return 0;
    }

    private static bool IsLikelyApiKey(string? token)
        => token is { Length: 64 } && token.All(Uri.IsHexDigit);

    private static string BuildRawEntryUrl(string baseUrl, string environment, string key)
        => $"{baseUrl.TrimEnd('/')}/api/{Uri.EscapeDataString(environment)}/{Uri.EscapeDataString(key)}";

    private static string? ReadLogicalContentType(HttpResponseMessage response)
        => response.Headers.TryGetValues(ConfigEntryValueRenderer.LogicalContentTypeHeader, out var values)
            ? values.FirstOrDefault()
            : null;
}
