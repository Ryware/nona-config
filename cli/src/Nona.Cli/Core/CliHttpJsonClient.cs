using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Nona.Cli;

internal sealed class CliHttpJsonClient(Func<HttpClient>? httpClientFactory = null)
{
    private readonly Func<HttpClient> _httpClientFactory = httpClientFactory ?? (() => new HttpClient());
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CliHttpJsonResult<T>> SendAsync<T>(
        NonaCliConnectionOptions connection,
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory();
        using var request = new HttpRequestMessage(method, BuildUri(connection.BaseUrl, path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(connection.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.BearerToken);

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, _jsonOptions),
                Encoding.UTF8,
                "application/json");
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new CliHttpJsonResult<T>(false, default, ReadError(responseBody) ?? response.ReasonPhrase ?? "Request failed", response.StatusCode);

        if (response.StatusCode == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(responseBody))
            return new CliHttpJsonResult<T>(true, default, null, response.StatusCode);

        var value = JsonSerializer.Deserialize<T>(responseBody, _jsonOptions);
        return new CliHttpJsonResult<T>(true, value, null, response.StatusCode);
    }

    private static Uri BuildUri(string baseUrl, string path)
    {
        var normalizedBaseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
        return new Uri(new Uri(normalizedBaseUrl), path.TrimStart('/'));
    }

    private static string? ReadError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}

internal sealed record CliHttpJsonResult<T>(bool Success, T? Value, string? Error, HttpStatusCode StatusCode);
