using Nona.Cli.Entries;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Tests.Entries;

public sealed class DotEnvEntryFormatterTests
{
    [Test]
    public async Task Format_WritesUnquotedKeyValueLines_WhenValueNeedsNoQuoting()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "Features:Checkout", Value = "true" }
        ]);

        await Assert.That(result).IsEqualTo("Features__Checkout=true");
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
            "Apple=1\nbanana=2\ncherry=3");
    }

    [Test]
    public async Task Format_ReplacesColonsInKeyWithDoubleUnderscore()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "Group:Sub:Key", Value = "x" }
        ]);

        await Assert.That(result).IsEqualTo("Group__Sub__Key=x");
    }

    [Test]
    public async Task Format_LeavesEmbeddedQuotesAndBackslashesUnescaped_WhenValueNeedsNoQuoting()
    {
        var result = DotEnvEntryFormatter.Format([
             new ConfigEntryDto { Key = "K", Value = "say \"hi\" \\ bye" }
         ]);

        await Assert.That(result).IsEqualTo("K=say \"hi\" \\ bye");
    }

    [Test]
    public async Task Format_SingleQuotesValue_WhenItHasLeadingOrTrailingWhitespace()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "K", Value = "  padded  " }
        ]);

        await Assert.That(result).IsEqualTo("K='  padded  '");
    }

    [Test]
    public async Task Format_SingleQuotesValue_WhenItContainsAHash()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "K", Value = "50% off#today" }
        ]);

        await Assert.That(result).IsEqualTo("K='50% off#today'");
    }

    [Test]
    public async Task Format_SingleQuotesValue_WhenItStartsWithAQuoteCharacter()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "K", Value = "\"quoted\" already" }
        ]);

        await Assert.That(result).IsEqualTo("K='\"quoted\" already'");
    }

    [Test]
    public async Task Format_DoubleQuotesAndEscapesValue_WhenItContainsBothQuoteCharacters()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "K", Value = "it's \"ok\"  " }
        ]);

        await Assert.That(result).IsEqualTo("K=\"it's \\\"ok\\\"  \"");
    }

    [Test]
    public async Task Format_TreatsNullValueAsEmptyString()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "K", Value = null }
        ]);

        await Assert.That(result).IsEqualTo("K=''");
    }

    [Test]
    public async Task Format_ReturnsEmptyString_WhenNoEntries()
    {
        var result = DotEnvEntryFormatter.Format([]);

        await Assert.That(result).IsEqualTo(string.Empty);
    }
}
