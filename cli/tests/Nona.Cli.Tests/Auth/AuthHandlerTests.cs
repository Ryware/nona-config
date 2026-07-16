using Nona.Cli.Auth.Commands;
using Nona.Cli.Auth.Queries;
using static Nona.Cli.Tests.TestHelpers;

namespace Nona.Cli.Tests.Auth;

public sealed class AuthHandlerTests
{
    [Test]
    public async Task RegisterFirstAdminCommandHandler_SavesSession_OnSuccess()
    {
        using var file = new TempFile();
        var store = new CliSessionStore(file.Path);
        const string response = """
            {
              "token": "jwt-token",
              "username": "admin@example.com",
              "role": "viewer",
              "expiresAt": "2026-06-04T12:00:00Z"
            }
            """;

        var result = await new RegisterFirstAdminCommandHandler(store, MockHttp(System.Net.HttpStatusCode.OK, response))
            .HandleAsync(new RegisterFirstAdminCommand("http://nona.test", "admin@example.com", "Password123!", SaveSession: true),
                CancellationToken.None);

        var session = store.Load();
        await Assert.That(result).IsEqualTo(0);
        await Assert.That(session).IsNotNull();
        await Assert.That(session!.BaseUrl).IsEqualTo("http://nona.test");
        await Assert.That(session.Token).IsEqualTo("jwt-token");
        await Assert.That(session.Username).IsEqualTo("admin@example.com");
    }

    [Test]
    public async Task RegisterFirstAdminCommandHandler_ReturnsOne_WhenRegistrationFails()
    {
        using var file = new TempFile();
        var store = new CliSessionStore(file.Path);
        const string response = """
            {
              "error": "User already exists"
            }
            """;

        var result = await new RegisterFirstAdminCommandHandler(store, MockHttp(System.Net.HttpStatusCode.Conflict, response))
            .HandleAsync(new RegisterFirstAdminCommand("http://nona.test", "admin@example.com", "Password123!", SaveSession: true),
                CancellationToken.None);

        await Assert.That(result).IsEqualTo(1);
        await Assert.That(store.Load()).IsNull();
    }

    [Test]
    public async Task LogoutCommandHandler_ClearsSession()
    {
        using var file = new TempFile();
        var store = new CliSessionStore(file.Path);
        store.Save(new CliAuthSession
        {
            BaseUrl = "http://x.com",
            Token = "tok",
            Username = "u",
            Role = "Admin",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            SavedAtUtc = DateTime.UtcNow
        });

        var result = await new LogoutCommandHandler(store).HandleAsync(new LogoutCommand(), CancellationToken.None);

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(store.Load()).IsNull();
    }

    [Test]
    public async Task WhoAmIQueryHandler_ReturnsZero_WhenNotLoggedIn()
    {
        var result = await new WhoAmIQueryHandler()
            .HandleAsync(new WhoAmIQuery(null), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task WhoAmIQueryHandler_ReturnsZero_WithActiveSession()
    {
        var session = new CliAuthSession
        {
            BaseUrl = "http://x.com",
            Token = "tok",
            Username = "admin",
            Role = "Admin",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            SavedAtUtc = DateTime.UtcNow
        };
        var result = await new WhoAmIQueryHandler()
            .HandleAsync(new WhoAmIQuery(session), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }
}
