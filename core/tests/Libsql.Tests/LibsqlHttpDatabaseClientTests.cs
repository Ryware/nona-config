using Nona.Libsql.Tests.Common;

namespace Nona.Libsql.Tests;

public class LibsqlHttpDatabaseClientTests
{
    [Test]
    public async Task ExecuteAsync_WrapsNetworkFailures()
    {
        using var httpClient = new HttpClient(new ThrowingHttpMessageHandler())
        {
            BaseAddress = new Uri("https://integration.test/")
        };

        var client = new LibsqlHttpDatabaseClient(httpClient);
        LibsqlException? exception = null;

        try
        {
            await client.ExecuteAsync("SELECT 1");
        }
        catch (LibsqlException ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message.Contains("libSQL", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task NormalizeBaseUrl_ConvertsLibsqlSchemeToHttps()
    {
        var normalized = LibsqlHttpDatabaseClient.NormalizeBaseUrl("libsql://example-db.turso.io/");
        await Assert.That(normalized).IsEqualTo("https://example-db.turso.io");
    }
}
