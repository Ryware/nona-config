using Nona.FirebaseRemoteConfigMigrator.DTOs;
using Nona.FirebaseRemoteConfigMigrator.Models;
using Nona.FirebaseRemoteConfigMigrator.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nona.FirebaseRemoteConfigMigrator;

internal sealed class NonaAdminClient(HttpClient httpClient, NonaOptions options)
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
            JsonContent.Create(new CreateProjectRequest(projectName), SerializerContext.Default.CreateProjectRequest),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Nona project create failed ({(int)response.StatusCode}): {content}");
        }

        var created = await response.Content.ReadFromJsonAsync(SerializerContext.Default.NonaProjectDto, cancellationToken);
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
            JsonContent.Create(new CreateEnvironmentRequest(environmentName), SerializerContext.Default.CreateEnvironmentRequest),
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
            JsonContent.Create(requestBody, SerializerContext.Default.UpsertConfigEntryRequest),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Nona config upsert failed ({(int)response.StatusCode}) for [{environmentName}] {key}: {content}");
        }
    }

    private async Task<IReadOnlyList<NonaProjectDto>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "admin/projects", null, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona projects fetch failed ({(int)response.StatusCode}): {content}");

        return JsonSerializer.Deserialize(content, SerializerContext.Default.NonaProjectDtoArray) ?? [];
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

        return JsonSerializer.Deserialize(content, SerializerContext.Default.NonaEnvironmentDtoArray) ?? [];
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

        using var response = await httpClient.PostAsJsonAsync(
            BuildUri("auth/login"),
            new LoginRequest(options.Email ?? string.Empty, options.Password ?? string.Empty),
            SerializerContext.Default.LoginRequest,
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona login failed ({(int)response.StatusCode}): {content}");

        var login = JsonSerializer.Deserialize(content, SerializerContext.Default.LoginResponse)
            ?? throw new InvalidOperationException("Nona login response empty.");

        bearerToken = login.Token;
        return bearerToken;
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
