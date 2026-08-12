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

public class PasswordResetEndpointTests
{
    private const string OldPassword = "Password123!";
    private const string NewPassword = "NewPassword123!";

    [Test]
    public async Task AdminCanGenerateAndUserCanConsumeOneTimeResetLink()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);
        var user = await CreateUserAsync(client, admin.Token, "member", completeInvitation: true);

        using var generateResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/admin/users/{user.Id}/password-reset",
            admin.Token);
        using var generateBody = await ParseJsonAsync(generateResponse);
        var resetToken = generateBody.RootElement.GetProperty("passwordResetToken").GetString();

        await Assert.That(generateResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(resetToken).IsNotNull();
        await Assert.That(generateBody.RootElement.GetProperty("expiresAt").GetDateTime())
            .IsGreaterThan(DateTime.UtcNow.AddHours(23));

        using (var detailsResponse = await client.GetAsync(
            $"/auth/password-resets/{Uri.EscapeDataString(resetToken!)}"))
        {
            using var details = await ParseJsonAsync(detailsResponse);
            await Assert.That(detailsResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(details.RootElement.GetProperty("email").GetString()).IsEqualTo(user.Email);
        }

        using (var completeResponse = await client.PostAsJsonAsync(
            $"/auth/password-resets/{Uri.EscapeDataString(resetToken!)}/password",
            new { newPassword = "x" }))
        {
            using var weakPasswordBody = await ParseJsonAsync(completeResponse);
            await Assert.That(completeResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That(weakPasswordBody.RootElement.GetProperty("errors").GetProperty("NewPassword")[0].GetString())
                .IsEqualTo("Password must be at least 8 characters long");
        }

        using (var completeResponse = await client.PostAsJsonAsync(
            $"/auth/password-resets/{Uri.EscapeDataString(resetToken!)}/password",
            new { newPassword = NewPassword }))
        {
            await Assert.That(completeResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }

        using (var reusedResponse = await client.GetAsync(
            $"/auth/password-resets/{Uri.EscapeDataString(resetToken!)}"))
        {
            using var reusedBody = await ParseJsonAsync(reusedResponse);
            await Assert.That(reusedResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(reusedBody.RootElement.GetProperty("errorCode").GetString())
                .IsEqualTo("password_reset_invalid_or_used");
        }

        using var oldLogin = await client.PostAsJsonAsync(
            "/auth/login",
            new { email = user.Email, password = OldPassword });
        using var newLogin = await client.PostAsJsonAsync(
            "/auth/login",
            new { email = user.Email, password = NewPassword });
        await Assert.That(oldLogin.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(newLogin.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task MemberIsForbiddenAndPendingAccountIsRejected()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);
        var member = await CreateUserAsync(client, admin.Token, "member", completeInvitation: true);
        var localUser = await CreateUserAsync(client, admin.Token, "member", completeInvitation: true);
        var pendingUser = await CreateUserAsync(client, admin.Token, "member", completeInvitation: false);

        using (var memberResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/admin/users/{localUser.Id}/password-reset",
            member.Token!))
        {
            await Assert.That(memberResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        }

        using (var pendingResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/admin/users/{pendingUser.Id}/password-reset",
            admin.Token))
        {
            using var pendingBody = await ParseJsonAsync(pendingResponse);
            await Assert.That(pendingResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
            await Assert.That(pendingBody.RootElement.GetProperty("errorCode").GetString())
                .IsEqualTo("password_reset_unavailable");
        }
    }

    [Test]
    public async Task AdminCannotGenerateResetLinkForOwnAccount()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var admin = await RegisterAdminAsync(client);

        using var usersResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/admin/users",
            admin.Token);
        usersResponse.EnsureSuccessStatusCode();
        using var usersBody = await ParseJsonAsync(usersResponse);
        var adminId = usersBody.RootElement
            .EnumerateArray()
            .Single(user => user.GetProperty("email").GetString() == admin.Email)
            .GetProperty("id")
            .GetInt64();

        using var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/admin/users/{adminId}/password-reset",
            admin.Token);
        using var body = await ParseJsonAsync(response);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(body.RootElement.GetProperty("errorCode").GetString())
            .IsEqualTo("password_reset_self_not_allowed");
    }

    private static async Task<Session> RegisterAdminAsync(HttpClient client)
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { email, password = OldPassword });
        response.EnsureSuccessStatusCode();
        using var body = await ParseJsonAsync(response);
        return new Session(
            0,
            email,
            body.RootElement.GetProperty("token").GetString()!);
    }

    private static async Task<Session> CreateUserAsync(
        HttpClient client,
        string adminToken,
        string role,
        bool completeInvitation)
    {
        var email = $"{role}-{Guid.NewGuid():N}@example.com";
        using var createResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/admin/users",
            adminToken,
            new { name = role, email, role, scope = "all" });
        createResponse.EnsureSuccessStatusCode();
        using var createBody = await ParseJsonAsync(createResponse);
        var id = createBody.RootElement.GetProperty("user").GetProperty("id").GetInt64();
        var invitationToken = createBody.RootElement.GetProperty("invitationToken").GetString()!;
        if (!completeInvitation)
        {
            return new Session(id, email, string.Empty);
        }

        using (var weakPasswordResponse = await client.PostAsJsonAsync(
            $"/auth/invitations/{Uri.EscapeDataString(invitationToken)}/password",
            new { newPassword = "x" }))
        {
            using var weakPasswordBody = await ParseJsonAsync(weakPasswordResponse);
            await Assert.That(weakPasswordResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That(weakPasswordBody.RootElement.GetProperty("errors").GetProperty("NewPassword")[0].GetString())
                .IsEqualTo("Password must be at least 8 characters long");
        }

        using var invitationResponse = await client.PostAsJsonAsync(
            $"/auth/invitations/{Uri.EscapeDataString(invitationToken)}/password",
            new { newPassword = OldPassword });
        invitationResponse.EnsureSuccessStatusCode();
        using var invitationBody = await ParseJsonAsync(invitationResponse);
        return new Session(
            id,
            email,
            invitationBody.RootElement.GetProperty("token").GetString()!);
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

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "InMemory",
            ["Jwt:Key"] = "password-reset-endpoint-tests-signing-key-1234567890",
            ["Jwt:Issuer"] = "password-reset-endpoint-tests",
            ["Jwt:Audience"] = "password-reset-endpoint-tests"
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

    private sealed record Session(long Id, string Email, string Token);
}
