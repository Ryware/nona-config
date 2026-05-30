using Nona.Migrator.Core.DTO;
using Nona.Migrator.Core.Models;
using Nona.Migrator.Core.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nona.Migrator.Core.Services;

public sealed class NonaAdminClient(HttpClient httpClient, NonaOptions options)
{
    private string? bearerToken;

    public async Task<string> EnsureProjectAsync(string projectName, CancellationToken cancellationToken)
    {
        var projects = await ListProjectsAsync(cancellationToken);
        var existing = projects.FirstOrDefault(project => string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing.Name;

        using var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "admin/projects",
            JsonContent.Create(new CreateProjectRequest(projectName), NonaSerializerContext.Default.CreateProjectRequest),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Nona project create failed ({(int)response.StatusCode}): {content}");
        }

        var created = await response.Content.ReadFromJsonAsync(NonaSerializerContext.Default.NonaProjectDto, cancellationToken);
        return created?.Name ?? projectName;
    }

    public async Task EnsureEnvironmentAsync(string projectName, string environmentName, CancellationToken cancellationToken)
    {
        var environments = await ListEnvironmentsAsync(projectName, cancellationToken);
        if (environments.Any(environment => string.Equals(environment.Name, environmentName, StringComparison.OrdinalIgnoreCase)))
            return;

        using var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"admin/projects/{Escape(projectName)}/environments",
            JsonContent.Create(new CreateEnvironmentRequest(environmentName), NonaSerializerContext.Default.CreateEnvironmentRequest),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Nona environment create failed ({(int)response.StatusCode}): {content}");
        }
    }

    public async Task UpsertConfigEntryAsync(string projectName, string environmentName, string key, UpsertConfigEntryRequest requestBody, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"admin/projects/{Escape(projectName)}/environments/{Escape(environmentName)}/config-entries/{Escape(key)}",
            JsonContent.Create(requestBody, NonaSerializerContext.Default.UpsertConfigEntryRequest),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Nona config upsert failed ({(int)response.StatusCode}) for [{environmentName}] {key}: {content}");
        }
    }

    public async Task<IReadOnlyList<NonaProjectDto>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "admin/projects", null, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona projects fetch failed ({(int)response.StatusCode}): {content}");

        return JsonSerializer.Deserialize(content, NonaSerializerContext.Default.NonaProjectDtoArray) ?? [];
    }

    public async Task<NonaProjectDto?> GetProjectAsync(string projectIdOrNameOrSlug, CancellationToken cancellationToken)
    {
        var projects = await ListProjectsAsync(cancellationToken);

        var byName = projects.FirstOrDefault(project =>
            string.Equals(project.Name, projectIdOrNameOrSlug, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
            return byName;

        if (long.TryParse(projectIdOrNameOrSlug, out var numericId))
        {
            var byId = projects.FirstOrDefault(project => project.Id == numericId);
            if (byId is not null)
                return byId;
        }

        return projects.FirstOrDefault(project =>
            !string.IsNullOrWhiteSpace(project.UrlSlug)
            && string.Equals(project.UrlSlug, projectIdOrNameOrSlug, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CreateUserResponse> CreateUserAsync(string name, string email, string? role, string? scope, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "admin/users",
            JsonContent.Create(new CreateUserRequest(name, email, role, scope), NonaSerializerContext.Default.CreateUserRequest),
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona user create failed ({(int)response.StatusCode}): {content}");

        return JsonSerializer.Deserialize(content, NonaSerializerContext.Default.CreateUserResponse)
            ?? throw new InvalidOperationException("Nona user create response empty.");
    }

    public async Task<NonaProjectDto> CreateProjectAsync(string name, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "admin/projects",
            JsonContent.Create(new CreateProjectRequest(name), NonaSerializerContext.Default.CreateProjectRequest),
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona project create failed ({(int)response.StatusCode}): {content}");

        return JsonSerializer.Deserialize(content, NonaSerializerContext.Default.NonaProjectDto)
            ?? throw new InvalidOperationException("Nona project create response empty.");
    }

    public async Task DeleteProjectAsync(string projectIdOrNameOrSlug, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"admin/projects/{Escape(projectIdOrNameOrSlug)}",
            null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Nona project delete failed ({(int)response.StatusCode}): {content}");
        }
    }

    public async Task<IReadOnlyList<ConfigEntryDto>> ListConfigEntriesAsync(
        string projectIdOrNameOrSlug,
        string environment,
        CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"admin/projects/{Escape(projectIdOrNameOrSlug)}/environments/{Escape(environment)}/config-entries",
            null,
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona config entries fetch failed ({(int)response.StatusCode}): {content}");

        return JsonSerializer.Deserialize(content, NonaSerializerContext.Default.ConfigEntryDtoArray) ?? [];
    }

    public async Task<ConfigEntryDto?> GetConfigEntryAsync(
        string projectIdOrNameOrSlug,
        string environment,
        string key,
        CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"admin/projects/{Escape(projectIdOrNameOrSlug)}/environments/{Escape(environment)}/config-entries/{Escape(key)}",
            null,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona config entry fetch failed ({(int)response.StatusCode}): {content}");

        return JsonSerializer.Deserialize(content, NonaSerializerContext.Default.ConfigEntryDto);
    }

    public async Task DeleteConfigEntryAsync(
        string projectIdOrNameOrSlug,
        string environment,
        string key,
        CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"admin/projects/{Escape(projectIdOrNameOrSlug)}/environments/{Escape(environment)}/config-entries/{Escape(key)}",
            null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Nona config entry delete failed ({(int)response.StatusCode}): {content}");
        }
    }

    public async Task<NonaProjectDto> RerollApiKeysAsync(string projectIdOrNameOrSlug, string keyType, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"admin/projects/{Escape(projectIdOrNameOrSlug)}/reroll-keys",
            JsonContent.Create(new RerollApiKeysRequest(keyType), NonaSerializerContext.Default.RerollApiKeysRequest),
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona API key reroll failed ({(int)response.StatusCode}): {content}");

        return JsonSerializer.Deserialize(content, NonaSerializerContext.Default.NonaProjectDto)
            ?? throw new InvalidOperationException("Nona API key reroll response empty.");
    }

    private async Task<IReadOnlyList<NonaEnvironmentDto>> ListEnvironmentsAsync(string projectName, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"admin/projects/{Escape(projectName)}/environments",
            null,
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona environments fetch failed ({(int)response.StatusCode}): {content}");

        return JsonSerializer.Deserialize(content, NonaSerializerContext.Default.NonaEnvironmentDtoArray) ?? [];
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string relativePath, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(relativePath))
        {
            Content = content
        };

        request.Headers.Authorization = new("Bearer", await GetBearerTokenAsync(cancellationToken));
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetBearerTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
            return bearerToken;

        if (!string.IsNullOrWhiteSpace(options.BearerToken))
        {
            bearerToken = options.BearerToken;
            return bearerToken;
        }

        var login = await LoginAsync(cancellationToken);
        return login.Token;
    }

    public async Task<LoginResponse> LoginAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            BuildUri("auth/login"),
            new LoginRequest(options.Email ?? string.Empty, options.Password ?? string.Empty),
            NonaSerializerContext.Default.LoginRequest,
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona login failed ({(int)response.StatusCode}): {content}");

        var login = JsonSerializer.Deserialize(content, NonaSerializerContext.Default.LoginResponse)
            ?? throw new InvalidOperationException("Nona login response empty.");

        bearerToken = login.Token;
        return login;
    }

    private Uri BuildUri(string relativePath)
    {
        var baseUrl = options.BaseUrl.TrimEnd('/');
        return new Uri($"{baseUrl}/{relativePath}");
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }
}
