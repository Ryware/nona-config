using Nona.Cli.Auth.Commands;
using Nona.Cli.Auth.Queries;
using static Nona.Cli.Tests.TestHelpers;

namespace Nona.Cli.Tests.Auth;

public sealed class AuthHandlerTests
{
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
