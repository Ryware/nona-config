using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Application;
using Nona.Infrastructure;
using Nona.WebApi;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class AdminReadAuthorizationTests
{
    private const string Password = "Password123!";

    [Test]
    public async Task Member_IsForbiddenFromEveryUsersAuditAndDashboardEndpoint()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);
        var member = await CreateUserAsync(client, admin.Token, "member", "member");

        var requests = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Post, "/admin/users", new { name = "Blocked", email = "blocked@example.com", role = "member", scope = "all" }),
            (HttpMethod.Get, "/admin/users", null),
            (HttpMethod.Get, $"/admin/users/{member.Id}", null),
            (HttpMethod.Put, $"/admin/users/{member.Id}", new { name = "Blocked", role = "member", scope = "all" }),
            (HttpMethod.Delete, $"/admin/users/{member.Id}", null),
            (HttpMethod.Get, $"/admin/users/{member.Id}/projects", null),
            (HttpMethod.Put, $"/admin/users/{member.Id}/projects/test", new { role = "viewer" }),
            (HttpMethod.Delete, $"/admin/users/{member.Id}/projects/test", null),
            (HttpMethod.Get, "/admin/audit-logs", null),
            (HttpMethod.Get, "/admin/audit-logs/export?format=csv", null),
            (HttpMethod.Get, "/admin/dashboard/counts", null)
        };

        foreach (var request in requests)
        {
            using var response = await SendAuthorizedAsync(
                client,
                request.Method,
                request.Path,
                member.Token,
                request.Body);

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
            await Assert.That(response.Content.Headers.ContentType?.MediaType)
                .IsEqualTo("application/problem+json");
        }
    }

    [Test]
    public async Task DemotedAdminToken_IsRejectedFromAdminReads()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);
        var adminId = await GetUserIdAsync(client, admin.Token, admin.Email);
        var otherAdmin = await CreateUserAsync(client, admin.Token, "admin", "other-admin");
        var sensitivePaths = SensitivePaths(adminId);

        await AssertSuccessfulAsync(client, otherAdmin.Token, sensitivePaths);

        using (var demoteResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            $"/admin/users/{otherAdmin.Id}",
            admin.Token,
            new { name = "Former Admin", role = "member", scope = "all" }))
        {
            await Assert.That(demoteResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        await AssertForbiddenAsync(client, otherAdmin.Token, sensitivePaths);
    }

    [Test]
    public async Task UnassignedMember_SeesNoProjectsAndDirectProjectAccessIsForbidden()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);
        var member = await CreateUserAsync(client, admin.Token, "member", "member");
        var projectName = $"project-{Guid.NewGuid():N}";

        using (var createProjectResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/admin/projects",
            admin.Token,
            new { name = projectName }))
        {
            await Assert.That(createProjectResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        }

        using (var adminListResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/admin/projects",
            admin.Token))
        {
            using var body = await ParseJsonAsync(adminListResponse);
            var project = body.RootElement.EnumerateArray()
                .Single(item => item.GetProperty("name").GetString() == projectName);
            await Assert.That(project.GetProperty("accessLevel").GetString()).IsEqualTo("admin");
        }

        using (var listResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/admin/projects",
            member.Token))
        {
            using var body = await ParseJsonAsync(listResponse);
            await Assert.That(listResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(body.RootElement.GetArrayLength()).IsEqualTo(0);
        }

        var requests = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Put, $"/admin/projects/{projectName}", new { name = $"{projectName}-renamed" }),
            (HttpMethod.Delete, $"/admin/projects/{projectName}", null),
            (HttpMethod.Get, $"/admin/projects/{projectName}/environments", null),
            (HttpMethod.Post, $"/admin/projects/{projectName}/environments", new { name = "development" }),
            (HttpMethod.Get, $"/admin/projects/{projectName}/environments/Production/config-entries", null),
            (HttpMethod.Put, $"/admin/projects/{projectName}/environments/Production/config-entries/sample", new { value = "value", contentType = "text", scope = "all" }),
            (HttpMethod.Get, $"/admin/projects/{projectName}/environments/Production/releases", null),
            (HttpMethod.Get, $"/admin/projects/{projectName}/api-keys", null)
        };

        foreach (var request in requests)
        {
            using var response = await SendAuthorizedAsync(
                client,
                request.Method,
                request.Path,
                member.Token,
                request.Body);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        }


        using (var assignViewerResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            $"/admin/users/{member.Id}/projects/{projectName}",
            admin.Token,
            new { role = "viewer" }))
        {
            await Assert.That(assignViewerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        await AssertProjectAccessLevelAsync(client, member.Token, projectName, "viewer");
        using (var viewerReadResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/admin/projects/{projectName}/environments",
            member.Token))
        {
            await Assert.That(viewerReadResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
        using (var viewerSecretResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/admin/projects/{projectName}/api-keys",
            member.Token))
        {
            await Assert.That(viewerSecretResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        }
        using (var viewerWriteResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/admin/projects/{projectName}/environments",
            member.Token,
            new { name = "viewer-environment" }))
        {
            await Assert.That(viewerWriteResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        }

        using (var assignEditorResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            $"/admin/users/{member.Id}/projects/{projectName}",
            admin.Token,
            new { role = "editor" }))
        {
            await Assert.That(assignEditorResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        await AssertProjectAccessLevelAsync(client, member.Token, projectName, "editor");
        using (var editorWriteResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/admin/projects/{projectName}/environments",
            member.Token,
            new { name = "editor-environment" }))
        {
            await Assert.That(editorWriteResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        }
        using (var editorSecretResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/admin/projects/{projectName}/api-keys",
            member.Token))
        {
            await Assert.That(editorSecretResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task UpdateAdmin_AcceptsUnchangedAdminRole()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);
        var adminId = await GetUserIdAsync(client, admin.Token, admin.Email);

        using var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            $"/admin/users/{adminId}",
            admin.Token,
            new { name = "Updated Admin", role = "admin", scope = "all" });
        using var body = await ParseJsonAsync(response);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.RootElement.GetProperty("name").GetString()).IsEqualTo("Updated Admin");
        await Assert.That(body.RootElement.GetProperty("role").GetString()).IsEqualTo("admin");
    }

    [Test]
    [Arguments("viewer")]
    [Arguments("editor")]
    public async Task LegacyOrganizationRoles_AreRejected(string role)
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);

        using var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/admin/users",
            admin.Token,
            new { name = "Legacy", email = $"{role}@example.com", role, scope = "all" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static IReadOnlyList<string> SensitivePaths(long targetUserId) =>
    [
        "/admin/users",
        $"/admin/users/{targetUserId}",
        $"/admin/users/{targetUserId}/projects",
        "/admin/audit-logs",
        "/admin/audit-logs/export?format=csv",
        "/admin/dashboard/counts"
    ];

    private static async Task AssertForbiddenAsync(HttpClient client, string token, IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            using var response = await SendAuthorizedAsync(client, HttpMethod.Get, path, token);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        }
    }

    private static async Task AssertSuccessfulAsync(HttpClient client, string token, IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            using var response = await SendAuthorizedAsync(client, HttpMethod.Get, path, token);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    private static async Task<UserSession> RegisterAdminAsync(HttpClient client)
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        using var response = await client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        using var body = await ParseJsonAsync(response);
        var token = body.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Register response did not include a token.");
        return new UserSession(0, email, token);
    }

    private static async Task<UserSession> CreateUserAsync(
        HttpClient client,
        string adminToken,
        string role,
        string emailPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        using var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/admin/users",
            adminToken,
            new { name = emailPrefix, email, role, scope = "all" });
        response.EnsureSuccessStatusCode();
        using var body = await ParseJsonAsync(response);
        var id = body.RootElement.GetProperty("user").GetProperty("id").GetInt64();
        var invitationToken = body.RootElement.GetProperty("invitationToken").GetString()
            ?? throw new InvalidOperationException("Create user response did not include an invitation token.");
        return new UserSession(id, email, await CompleteInvitationAsync(client, invitationToken));
    }

    private static async Task<string> CompleteInvitationAsync(HttpClient client, string invitationToken)
    {
        using var response = await client.PostAsJsonAsync(
            $"/auth/invitations/{Uri.EscapeDataString(invitationToken)}/password",
            new { newPassword = Password });
        response.EnsureSuccessStatusCode();
        using var body = await ParseJsonAsync(response);
        return body.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Invitation response did not include a token.");
    }

    private static async Task<long> GetUserIdAsync(HttpClient client, string token, string email)
    {
        using var response = await SendAuthorizedAsync(client, HttpMethod.Get, "/admin/users", token);
        response.EnsureSuccessStatusCode();
        using var body = await ParseJsonAsync(response);
        return body.RootElement.EnumerateArray()
            .Single(user => string.Equals(user.GetProperty("email").GetString(), email, StringComparison.OrdinalIgnoreCase))
            .GetProperty("id")
            .GetInt64();
    }

    private static async Task AssertProjectAccessLevelAsync(
        HttpClient client,
        string token,
        string projectName,
        string expectedAccessLevel)
    {
        using var response = await SendAuthorizedAsync(client, HttpMethod.Get, "/admin/projects", token);
        using var body = await ParseJsonAsync(response);
        var project = body.RootElement.EnumerateArray().Single();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(project.GetProperty("name").GetString()).IsEqualTo(projectName);
        await Assert.That(project.GetProperty("accessLevel").GetString()).IsEqualTo(expectedAccessLevel);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "InMemory",
            ["Jwt:Key"] = "admin-read-authorization-tests-signing-key-1234567890",
            ["Jwt:Issuer"] = "admin-read-authorization-tests",
            ["Jwt:Audience"] = "admin-read-authorization-tests"
        });
        builder.Services.AddOpenApi();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();
        await app.StartAsync();
        return app;
    }

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private sealed record UserSession(long Id, string Email, string Token);
}
