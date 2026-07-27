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
    public async Task Viewer_IsForbiddenFromSensitiveAdminReadsButCanReadSelf()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);
        var adminId = await GetUserIdAsync(client, admin.Token, admin.Email);
        var viewer = await CreateUserAsync(client, admin.Token, "viewer", "viewer");

        var sensitivePaths = SensitivePaths(adminId);
        await AssertForbiddenAsync(client, viewer.Token, sensitivePaths);

        using (var selfResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/admin/users/{viewer.Id}",
            viewer.Token))
        {
            await Assert.That(selfResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        using (var selfProjectsResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/admin/users/{viewer.Id}/projects",
            viewer.Token))
        {
            await Assert.That(selfProjectsResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task DeletedEditorToken_IsRejectedFromSensitiveAdminReads()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);
        var adminId = await GetUserIdAsync(client, admin.Token, admin.Email);
        var editor = await CreateUserAsync(client, admin.Token, "editor", "editor");
        var sensitivePaths = SensitivePaths(adminId);

        await AssertSuccessfulAsync(client, editor.Token, sensitivePaths);

        using (var deleteResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Delete,
            $"/admin/users/{editor.Id}",
            admin.Token))
        {
            await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }

        await AssertForbiddenAsync(client, editor.Token, sensitivePaths);
    }

    private static IReadOnlyList<string> SensitivePaths(long targetUserId)
    {
        return
        [
            "/admin/users",
            $"/admin/users/{targetUserId}",
            $"/admin/users/{targetUserId}/projects",
            "/admin/audit-logs",
            "/admin/dashboard/counts"
        ];
    }

    private static async Task AssertForbiddenAsync(
        HttpClient client,
        string token,
        IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            using var response = await SendAuthorizedAsync(client, HttpMethod.Get, path, token);

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
            await Assert.That(response.Content.Headers.ContentType?.MediaType)
                .IsEqualTo("application/problem+json");
        }
    }

    private static async Task AssertSuccessfulAsync(
        HttpClient client,
        string token,
        IEnumerable<string> paths)
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

        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { email, password = Password });
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
        var token = await CompleteInvitationAsync(client, invitationToken);

        return new UserSession(id, email, token);
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
        return body.RootElement
            .EnumerateArray()
            .Single(user => string.Equals(
                user.GetProperty("email").GetString(),
                email,
                StringComparison.OrdinalIgnoreCase))
            .GetProperty("id")
            .GetInt64();
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
