using Nona.Migrator.Core.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Nona.Migrator.Core.Services;

public sealed class NonaAdminClient
{
    private readonly HttpClient _httpClient;
    private readonly NonaOptions _options;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public NonaAdminClient(HttpClient httpClient, NonaOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<NonaAdminProject?> GetProjectAsync(string idOrNameOrSlug, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "admin/projects");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var projects = JsonSerializer.Deserialize<List<NonaAdminProject>>(body, _jsonOptions) ?? [];

        return projects.FirstOrDefault(project =>
            string.Equals(project.Name, idOrNameOrSlug, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project.UrlSlug, idOrNameOrSlug, StringComparison.OrdinalIgnoreCase));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(BuildBaseUri(), path));
        if (!string.IsNullOrWhiteSpace(_options.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private Uri BuildBaseUri()
    {
        var baseUrl = _options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? _options.BaseUrl
            : _options.BaseUrl + "/";

        return new Uri(baseUrl, UriKind.Absolute);
    }

}

public sealed class NonaAdminProject
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UrlSlug { get; set; }
    public List<string> Environments { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
