using Nona.Cli.Entries;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Tests.Entries;

public sealed class DotEnvEntryFormatterTests
{
    [Test]
    [Arguments("Group:Flag", "Group__Flag", "Group__Flag")]
    [Arguments("Group:Flag", "group__flag", "Group__Flag")]
    [Arguments("Group:Sub:Flag", "Group__Sub:Flag", "Group__Sub__Flag")]
    [Arguments("Group__Flag", "group__flag", "Group__Flag")]
    public async Task Format_RejectsConflictingKeys_EvenWhenValuesMatch(string firstKey, string secondKey, string exportedKey)
    {
        var error = CaptureFormatError([
            new ConfigEntryDto { Key = firstKey, Value = "same-secret-value" },
            new ConfigEntryDto { Key = secondKey, Value = "same-secret-value" }
        ]);

        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).IsEqualTo(
            $"Cannot export dotenv: keys '{firstKey}', '{secondKey}' map to duplicate key '{exportedKey}'.");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Format_ReportsAllConflictsInStableOrder_WithoutValues(bool reverse)
    {
        ConfigEntryDto[] entries = [
            new() { Key = "Z:Flag", Value = "secret-one" },
            new() { Key = "z__flag", Value = "secret-two" },
            new() { Key = "A__Sub__Flag", Value = "secret-three" },
            new() { Key = "A:Sub__Flag", Value = "secret-four" },
            new() { Key = "A:Sub:Flag", Value = "secret-five" },
            new() { Key = "Safe", Value = "secret-six" }
        ];
        var error = CaptureFormatError(reverse ? entries.Reverse() : entries);

        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).IsEqualTo(
            "Cannot export dotenv: keys 'A:Sub:Flag', 'A:Sub__Flag', 'A__Sub__Flag' map to duplicate key 'A__Sub__Flag'; "
            + "keys 'Z:Flag', 'z__flag' map to duplicate key 'Z__Flag'.");
    }

    [Test]
    public async Task Format_EnumeratesEntriesOnce()
    {
        var enumerated = false;
        IEnumerable<ConfigEntryDto> Entries()
        {
            if (enumerated)
                throw new InvalidOperationException("Entries were enumerated twice.");
            enumerated = true;
            yield return new ConfigEntryDto { Key = "Group:Flag", Value = "true" };
        }

        var result = DotEnvEntryFormatter.Format(Entries());

        await Assert.That(result).IsEqualTo("Group__Flag=true");
    }

    private static DotEnvKeyCollisionException? CaptureFormatError(IEnumerable<ConfigEntryDto> entries)
    {
        try
        {
            DotEnvEntryFormatter.Format(entries);
            return null;
        }
        catch (DotEnvKeyCollisionException error)
        {
            return error;
        }
    }

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
