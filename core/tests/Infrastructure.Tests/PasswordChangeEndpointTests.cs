using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Application;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Infrastructure;
using Nona.WebApi;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class PasswordChangeEndpointTests
{
    private const string OldPassword = "Password123!";
    private const string NewPassword = "NewPassword123!";

    [Test]
    public async Task PasswordUserCanChangePasswordAndKeepCurrentToken()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var session = await RegisterAsync(client);

        using (var accountResponse = await SendAuthorizedAsync(client, HttpMethod.Get, "/auth/me", session.Token))
        {
            using var account = await ParseJsonAsync(accountResponse);
            await Assert.That(accountResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(account.RootElement.GetProperty("email").GetString()).IsEqualTo(session.Email);
            await Assert.That(account.RootElement.GetProperty("passwordEnabled").GetBoolean()).IsTrue();
        }

        using (var wrongResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/auth/password",
            session.Token,
            new { currentPassword = "wrong", newPassword = NewPassword }))
        {
            using var wrong = await ParseJsonAsync(wrongResponse);
            await Assert.That(wrongResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That(wrong.RootElement.GetProperty("errorCode").GetString())
                .IsEqualTo("current_password_invalid");
        }

        using (var weakPasswordResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/auth/password",
            session.Token,
            new { currentPassword = OldPassword, newPassword = "x" }))
        {
            using var weakPassword = await ParseJsonAsync(weakPasswordResponse);
            await Assert.That(weakPasswordResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That(weakPassword.RootElement.GetProperty("errors").GetProperty("NewPassword")[0].GetString())
                .IsEqualTo("Password must be at least 8 characters long");
        }

        using (var sameResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/auth/password",
            session.Token,
            new { currentPassword = OldPassword, newPassword = OldPassword }))
        {
            using var same = await ParseJsonAsync(sameResponse);
            await Assert.That(sameResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That(same.RootElement.GetProperty("errorCode").GetString())
                .IsEqualTo("new_password_must_differ");
        }

        using (var changeResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/auth/password",
            session.Token,
            new { currentPassword = OldPassword, newPassword = NewPassword }))
        {
            await Assert.That(changeResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }

        using (var existingTokenResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/auth/me",
            session.Token))
        {
            await Assert.That(existingTokenResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        using var oldLogin = await client.PostAsJsonAsync(
            "/auth/login",
            new { email = session.Email, password = OldPassword });
        using var newLogin = await client.PostAsJsonAsync(
            "/auth/login",
            new { email = session.Email, password = NewPassword });
        await Assert.That(oldLogin.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(newLogin.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task PasswordlessAccountIsRejectedAndAnonymousRequestIsUnauthorized()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var repository = app.Services.GetRequiredService<IUserRepository>();
        var tokenService = app.Services.GetRequiredService<IJwtTokenService>();
        var now = DateTime.UtcNow;
        var passwordless = new User
        {
            Email = $"sso-{Guid.NewGuid():N}@example.com",
            Name = "SSO User",
            Role = UserRole.Member,
            CreatedAt = now,
            UpdatedAt = now
        };
        await repository.AddAsync(passwordless);
        var token = tokenService.GenerateToken(passwordless);

        using (var passwordlessResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            "/auth/password",
            token,
            new { currentPassword = "current", newPassword = NewPassword }))
        {
            using var body = await ParseJsonAsync(passwordlessResponse);
            await Assert.That(passwordlessResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
            await Assert.That(body.RootElement.GetProperty("errorCode").GetString())
                .IsEqualTo("password_change_unavailable");
        }

        using var anonymousResponse = await client.PutAsJsonAsync(
            "/auth/password",
            new { currentPassword = "current", newPassword = NewPassword });
        await Assert.That(anonymousResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    private static async Task<Session> RegisterAsync(HttpClient client)
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { email, password = OldPassword });
        response.EnsureSuccessStatusCode();
        using var body = await ParseJsonAsync(response);
        return new Session(email, body.RootElement.GetProperty("token").GetString()!);
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
            ["Jwt:Key"] = "password-change-endpoint-tests-signing-key-1234567890",
            ["Jwt:Issuer"] = "password-change-endpoint-tests",
            ["Jwt:Audience"] = "password-change-endpoint-tests"
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

    private sealed record Session(string Email, string Token);
}
