using Nona.Cli.Entries;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Tests.Entries;

public sealed class DotEnvEntryFormatterTests
{
    [Test]
    public async Task Format_WritesQuotedKeyValueLines()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "Features:Checkout", Value = "true" }
        ]);

        await Assert.That(result).IsEqualTo("Features:Checkout=\"true\"");
    }

    [Test]
    public async Task Format_SortsByKeyOrdinalIgnoreCase()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "banana", Value = "2" },
            new ConfigEntryDto { Key = "Apple", Value = "1" },
            new ConfigEntryDto { Key = "cherry", Value = "3" }
        ]);

        await Assert.That(result).IsEqualTo(
            "Apple=\"1\"\nbanana=\"2\"\ncherry=\"3\"");
    }

    [Test]
    public async Task Format_KeepsColonsInKeyLiteral()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "Group:Sub:Key", Value = "x" }
        ]);

        await Assert.That(result).IsEqualTo("Group:Sub:Key=\"x\"");
    }

    [Test]
    public async Task Format_EscapesQuotesAndBackslashesInValue()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "K", Value = """say "hi" \ bye""" }
        ]);

        await Assert.That(result).IsEqualTo("K=\"say \\\"hi\\\" \\\\ bye\"");
    }

    [Test]
    public async Task Format_TreatsNullValueAsEmptyString()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "K", Value = null }
        ]);

        await Assert.That(result).IsEqualTo("K=\"\"");
    }

    [Test]
    public async Task Format_ReturnsEmptyString_WhenNoEntries()
    {
        var result = DotEnvEntryFormatter.Format([]);

        await Assert.That(result).IsEqualTo(string.Empty);
    }
}
