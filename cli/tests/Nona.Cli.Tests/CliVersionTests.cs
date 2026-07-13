namespace Nona.Cli.Tests;

[NotInParallel]
public sealed class CliVersionTests
{
    [Test]
    public async Task GetDisplayVersion_UsesEnvironmentOverrideWhenPresent()
    {
        using var scope = new TestHelpers.EnvironmentScope(new Dictionary<string, string?>
        {
            ["NONA_CLI_VERSION"] = "0.0.3"
        });

        await Assert.That(CliVersion.GetDisplayVersion()).IsEqualTo("0.0.3");
    }

    [Test]
    public async Task GetDisplayVersion_StripsBuildMetadata()
    {
        using var scope = new TestHelpers.EnvironmentScope(new Dictionary<string, string?>
        {
            ["NONA_CLI_VERSION"] = null
        });

        var version = CliVersion.GetDisplayVersion();

        await Assert.That(string.IsNullOrEmpty(version)).IsFalse();
        await Assert.That(version.Contains('+')).IsFalse();
    }

    [Test]
    public async Task IsVersionRequest_AcceptsLongAndShortFlags()
    {
        await Assert.That(CliVersion.IsVersionRequest(["--version"])).IsTrue();
        await Assert.That(CliVersion.IsVersionRequest(["-v"])).IsTrue();
        await Assert.That(CliVersion.IsVersionRequest(["keys", "show"])).IsFalse();
    }
}
