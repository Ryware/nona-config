namespace Nona.Cli.Tests;

public sealed class CliDefaultsStoreTests
{
    [Test]
    public async Task SaveAndLoad_RoundTripsDefaults()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"nona-cli-defaults-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var configPath = Path.Combine(tempDirectory, "config.json");
            var store = new CliDefaultsStore(configPath);
            var defaults = new CliDefaults
            {
                BaseUrl = "http://nona.internal:18080",
                Project = "mobile-app"
            };

            store.Save(defaults);
            var loaded = store.Load();

            await Assert.That(loaded.BaseUrl).IsEqualTo("http://nona.internal:18080");
            await Assert.That(loaded.Project).IsEqualTo("mobile-app");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
