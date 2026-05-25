namespace Nona.Cli.Tests;

public sealed class CliSessionStoreTests
{
    [Test]
    public async Task SaveAndLoad_RoundTripsSession()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"nona-cli-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var sessionPath = Path.Combine(tempDirectory, "session.json");
            var store = new CliSessionStore(sessionPath);
            var session = new CliAuthSession
            {
                BaseUrl = "http://nona.internal:18080",
                Token = "token-123",
                Username = "admin@example.com",
                Role = "Admin",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                SavedAtUtc = DateTime.UtcNow
            };

            store.Save(session);
            var loaded = store.Load();

            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.BaseUrl).IsEqualTo("http://nona.internal:18080");
            await Assert.That(loaded.Token).IsEqualTo("token-123");
            await Assert.That(loaded.Username).IsEqualTo("admin@example.com");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
