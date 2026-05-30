using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nona.Cli;

internal sealed class NonaApiClient(string baseUrl, string token, HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly string _base = baseUrl.TrimEnd('/');

    public async Task<IReadOnlyList<ProjectDto>> ListProjectsAsync(CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, "admin/projects", null, ct);
        await EnsureSuccessAsync(response, "list projects", ct);
        return await response.Content.ReadFromJsonAsync<ProjectDto[]>(JsonOpts, ct) ?? [];
    }

    public async Task<ProjectDto?> GetProjectAsync(string nameOrSlug, CancellationToken ct)
    {
        var projects = await ListProjectsAsync(ct);
        return projects.FirstOrDefault(p =>
            string.Equals(p.Name, nameOrSlug, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.UrlSlug, nameOrSlug, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ProjectDto> CreateProjectAsync(string name, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "admin/projects",
            JsonContent.Create(new { name }, options: JsonOpts), ct);
        await EnsureSuccessAsync(response, "create project", ct);
        return await response.Content.ReadFromJsonAsync<ProjectDto>(JsonOpts, ct)
               ?? throw new InvalidOperationException("Empty response from project create.");
    }

    public async Task DeleteProjectAsync(string project, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"admin/projects/{E(project)}", null, ct);
        await EnsureSuccessAsync(response, "delete project", ct);
    }

    public async Task<ProjectDto> RerollApiKeysAsync(string project, string keyType, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post,
            $"admin/projects/{E(project)}/reroll-keys",
            JsonContent.Create(new { keyType }, options: JsonOpts), ct);
        await EnsureSuccessAsync(response, "reroll API keys", ct);
        return await response.Content.ReadFromJsonAsync<ProjectDto>(JsonOpts, ct)
               ?? throw new InvalidOperationException("Empty response from key reroll.");
    }

    public async Task<IReadOnlyList<ConfigEntryDto>> ListConfigEntriesAsync(
        string project, string environment, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get,
            $"admin/projects/{E(project)}/environments/{E(environment)}/config-entries", null, ct);
        await EnsureSuccessAsync(response, "list config entries", ct);
        return await response.Content.ReadFromJsonAsync<ConfigEntryDto[]>(JsonOpts, ct) ?? [];
    }

    public async Task<ConfigEntryDto?> GetConfigEntryAsync(
        string project, string environment, string key, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get,
            $"admin/projects/{E(project)}/environments/{E(environment)}/config-entries/{E(key)}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, "get config entry", ct);
        return await response.Content.ReadFromJsonAsync<ConfigEntryDto>(JsonOpts, ct);
    }

    public async Task UpsertConfigEntryAsync(
        string project, string environment, string key,
        string value, string? scope, string? contentType, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Put,
            $"admin/projects/{E(project)}/environments/{E(environment)}/config-entries/{E(key)}",
            JsonContent.Create(new { value, contentType, scope }, options: JsonOpts), ct);
        await EnsureSuccessAsync(response, "set config entry", ct);
    }

    public async Task DeleteConfigEntryAsync(
        string project, string environment, string key, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Delete,
            $"admin/projects/{E(project)}/environments/{E(environment)}/config-entries/{E(key)}", null, ct);
        await EnsureSuccessAsync(response, "delete config entry", ct);
    }

    public async Task<CreatedUserDto> CreateUserAsync(
        string name, string email, string? role, string? scope, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "admin/users",
            JsonContent.Create(new { name, email, role, scope }, options: JsonOpts), ct);
        await EnsureSuccessAsync(response, "create user", ct);
        var body = await response.Content.ReadFromJsonAsync<CreateUserApiResponse>(JsonOpts, ct)
                   ?? throw new InvalidOperationException("Empty response from user create.");
        return new CreatedUserDto
        {
            Name = body.User.Name, Email = body.User.Email,
            Role = body.User.Role, Scope = body.User.Scope,
            InvitationToken = body.InvitationToken
        };
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var req = new HttpRequestMessage(method, $"{_base}/{path}") { Content = content };
        req.Headers.Authorization = new("Bearer", token);
        return http.SendAsync(req, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage res, string operation, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var body = await res.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Nona {operation} failed ({(int)res.StatusCode}): {body}");
    }

    private static string E(string v) => Uri.EscapeDataString(v);

    private sealed class CreateUserApiResponse
    {
        public CreateUserInfo User { get; init; } = new();
        public string InvitationToken { get; init; } = string.Empty;
    }

    private sealed class CreateUserInfo
    {
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Scope { get; init; } = string.Empty;
    }
}
