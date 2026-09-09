using Microsoft.Kiota.Abstractions;
using Nona.Cli.Entries;
using Nona.Cli.Generated.Models;
using System.Net;
using System.Text.Json;

namespace Nona.Cli.Entries.Queries;

internal sealed record GetEntryQuery(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Key,
    bool UseReleases = false,
    string? ReleaseVersion = null);

internal sealed class GetEntryQueryHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(GetEntryQuery query, CancellationToken ct)
    {
        var releaseVersion = query.UseReleases
            ? NormalizeReleaseVersion(query.ReleaseVersion)
            : null;

        if (releaseVersion is "." or "..")
        {
            Console.Error.WriteLine("--release-version cannot be '.' or '..'.");
            return CliExitCodes.ValidationError;
        }

        var usesApiKey = IsLikelyApiKey(query.Connection.BearerToken);
        if (query.UseReleases && !usesApiKey)
        {
            Console.Error.WriteLine("--use-releases requires a Nona API key. Admin bearer tokens can only read working entries.");
            return CliExitCodes.ValidationError;
        }

        if (usesApiKey)
            return await GetRawEntryAsync(query, releaseVersion, ct);

        return await GetAdminEntryAsync(query, ct);
    }

    private async Task<int> GetRawEntryAsync(
        GetEntryQuery query,
        string? releaseVersion,
        CancellationToken ct)
    {
        using var http = httpClientFactory?.Invoke() ?? new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildRawEntryUrl(
                query.Connection.BaseUrl,
                query.Environment,
                query.Key,
                query.UseReleases,
                releaseVersion));

        request.Headers.TryAddWithoutValidation("X-Api-Key", query.Connection.BearerToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw await ReadProblemDetailsAsync(response, ct);

        var value = await response.Content.ReadAsStringAsync(ct);
        var contentType = ReadLogicalContentType(response);
        ConfigEntryValueRenderer.WriteValue(value, contentType);
        return 0;
    }

    private async Task<int> GetAdminEntryAsync(GetEntryQuery query, CancellationToken ct)
    {
        using var api = NonaClientFactory.Create(query.Connection, httpClientFactory);

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

    private static string BuildRawEntryUrl(
        string baseUrl,
        string environment,
        string key,
        bool useReleases,
        string? releaseVersion)
    {
        var sourcePath = !useReleases
            ? "parameters"
            : releaseVersion is null
                ? "releases/active/parameters"
                : $"releases/{Uri.EscapeDataString(releaseVersion)}/parameters";
        return $"{baseUrl.TrimEnd('/')}/api/environments/{Uri.EscapeDataString(environment)}/{sourcePath}/{Uri.EscapeDataString(key)}";
    }

    private static string? NormalizeReleaseVersion(string? releaseVersion)
        => string.IsNullOrWhiteSpace(releaseVersion) ? null : releaseVersion.Trim();

    private static async Task<ApiProblemDetails> ReadProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(ct);

        ApiProblemDetails? problem = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                problem = JsonSerializer.Deserialize<ApiProblemDetails>(
                    body,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                // Fall through to a status-based error when the server does not return ProblemDetails.
            }
        }

        problem ??= new ApiProblemDetails
        {
            Detail = response.ReasonPhrase ?? "Request failed"
        };
        problem.Status ??= (int)response.StatusCode;
        problem.ResponseStatusCode = (int)response.StatusCode;
        return problem;
    }

    private static string? ReadLogicalContentType(HttpResponseMessage response)
        => response.Headers.TryGetValues(ConfigEntryValueRenderer.LogicalContentTypeHeader, out var values)
            ? values.FirstOrDefault()
            : null;
}
