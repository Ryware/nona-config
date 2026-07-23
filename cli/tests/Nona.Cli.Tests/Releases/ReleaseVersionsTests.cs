using Nona.Cli.Releases;

namespace Nona.Cli.Tests.Releases;

public sealed class ReleaseVersionsTests
{
    [Test]
    [Arguments("0.0", 0, 0)]
    [Arguments("1.2", 1, 2)]
    [Arguments("01.002", 1, 2)]
    public async Task TryParseLine_AcceptsOnlyMajorMinor(
        string input,
        int expectedMajor,
        int expectedMinor)
    {
        var parsed = ReleaseVersions.TryParseLine(input, out var version);

        await Assert.That(parsed).IsTrue();
        await Assert.That(version.Major).IsEqualTo(expectedMajor);
        await Assert.That(version.Minor).IsEqualTo(expectedMinor);
        await Assert.That(version.FirstRelease.ToString())
            .IsEqualTo($"{expectedMajor}.{expectedMinor}.0");
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments(" 1.2")]
    [Arguments("1.2 ")]
    [Arguments("1")]
    [Arguments("1.2.0")]
    [Arguments("1.2.x")]
    [Arguments("1.-2")]
    [Arguments("+1.2")]
    [Arguments("1..2")]
    [Arguments("2147483648.0")]
    public async Task TryParseLine_RejectsInvalidManagementInput(string input)
    {
        await Assert.That(ReleaseVersions.TryParseLine(input, out _)).IsFalse();
    }

    [Test]
    [Arguments("0.0.0", 0, 0, 0)]
    [Arguments("1.2.3", 1, 2, 3)]
    [Arguments("01.002.0003", 1, 2, 3)]
    public async Task TryParseExact_NormalizesExactVersions(
        string input,
        int expectedMajor,
        int expectedMinor,
        int expectedPatch)
    {
        var parsed = ReleaseVersions.TryParseExact(input, out var version);

        await Assert.That(parsed).IsTrue();
        await Assert.That(version.Major).IsEqualTo(expectedMajor);
        await Assert.That(version.Minor).IsEqualTo(expectedMinor);
        await Assert.That(version.Patch).IsEqualTo(expectedPatch);
        await Assert.That(version.ToString())
            .IsEqualTo($"{expectedMajor}.{expectedMinor}.{expectedPatch}");
    }

    [Test]
    [Arguments("")]
    [Arguments("1.2")]
    [Arguments("1.2.x")]
    [Arguments("1.2.3 ")]
    [Arguments("1.2.3.4")]
    [Arguments("1.2.-1")]
    [Arguments("1.2.2147483648")]
    public async Task TryParseExact_RejectsInvalidManagementInput(string input)
    {
        await Assert.That(ReleaseVersions.TryParseExact(input, out _)).IsFalse();
    }

    [Test]
    public async Task TryGetNextPatch_UsesMaximumPatchFromOnlyTheSourceLine()
    {
        var source = new ReleaseVersion(1, 2, 0);
        var existing = new[] { "1.2.1", "1.2.3", "1.1.99", "2.2.50", "invalid" };

        var success = ReleaseVersions.TryGetNextPatch(source, existing, out var next);

        await Assert.That(success).IsTrue();
        await Assert.That(next.ToString()).IsEqualTo("1.2.4");
    }

    [Test]
    public async Task TryGetNextPatch_RejectsPatchOverflow()
    {
        var source = new ReleaseVersion(1, 2, int.MaxValue);

        await Assert.That(ReleaseVersions.TryGetNextPatch(source, [], out _)).IsFalse();
    }
}
