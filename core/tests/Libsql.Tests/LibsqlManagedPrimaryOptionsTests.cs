namespace Nona.Libsql.Tests;

public class LibsqlManagedPrimaryOptionsTests
{
    [Test]
    public async Task ResolveLocalConnectUrl_UsesExplicitOverride_WhenProvided()
    {
        var options = new LibsqlManagedPrimaryOptions
        {
            LocalConnectUrl = "http://localhost:9999",
            HttpListenAddress = "0.0.0.0:9080"
        };

        await Assert.That(options.ResolveLocalConnectUrl()).IsEqualTo("http://localhost:9999");
    }

    [Test]
    public async Task ResolveLocalConnectUrl_NormalizesWildcardListenAddress()
    {
        var options = new LibsqlManagedPrimaryOptions
        {
            HttpListenAddress = "0.0.0.0:9080"
        };

        await Assert.That(options.ResolveLocalConnectUrl()).IsEqualTo("http://127.0.0.1:9080");
    }

    [Test]
    public async Task ResolveWorkingDirectory_DefaultsToDatabaseDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nona-libsql-options-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "data", "primary.db");

        var options = new LibsqlManagedPrimaryOptions
        {
            DatabasePath = databasePath
        };

        await Assert.That(options.ResolveWorkingDirectory()).IsEqualTo(Path.GetDirectoryName(databasePath));
    }
}
