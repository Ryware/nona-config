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
    public async Task Format_UsesBackticks_WhenItContainsBothQuoteCharacters()
    {
        var result = DotEnvEntryFormatter.Format([
            new ConfigEntryDto { Key = "K", Value = "it's \"ok\"  " }
        ]);

        await Assert.That(result).IsEqualTo("K=`it's \"ok\"  `");
    }

    [Test]
    [Arguments("`hello`", "'`hello`'")]
    [Arguments("it's #ok", "`it's #ok`")]
    [Arguments("it's `ok`#", "\"it's `ok`#\"")]
    [Arguments("it's `ok`\\r#", "\"it's `ok`\\r#\"")]
    [Arguments("line\none", "'line\none'")]
    [Arguments("line'\none", "`line'\none`")]
    [Arguments("line'`\none", "\"line'`\none\"")]
    [Arguments("\\n\\r\\\\", "\\n\\r\\\\")]
    [Arguments("trailing'", "trailing'")]
    [Arguments("a'\"`b", "a'\"`b")]
    [Arguments("\u00a0hello\u00a0", "\u00a0hello\u00a0")]
    [Arguments("\t hello\t", "'\t hello\t'")]
    [Arguments("${VAR}", "${VAR}")]
    [Arguments("\0😀", "\0😀")]
    [Arguments("", "''")]
    public async Task Format_PreservesValueWithoutBackslashEscaping(string value, string representation)
    {
        await Assert.That(DotEnvEntryFormatter.Format([new() { Key = "K", Value = value }]))
            .IsEqualTo($"K={representation}");
    }

    [Test]
    [Arguments("a\rsecret", "carriage return")]
    [Arguments("a'\"`#secret", "quote")]
    [Arguments("a'`\\n#secret", "quote")]
    public async Task Format_RejectsUnrepresentableValueWithoutDisclosingIt(string value, string reason)
    {
        var error = CaptureValidationError([new() { Key = "Secret:Key", Value = value }]);
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).Contains("Secret:Key");
        await Assert.That(error.Message).Contains(reason);
        await Assert.That(error.Message.Contains("secret", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Format_RejectsUnpairedSurrogatesInKeysAndValues()
    {
        foreach (var invalid in new[] { "\ud800", "\udfff", "\ud800x", "x\udfff", "\udfff\ud800" })
        {
            var keyError = CaptureValidationError([new() { Key = "K" + invalid, Value = "hidden" }]);
            var valueError = CaptureValidationError([new() { Key = "K", Value = invalid + "hidden" }]);
            await Assert.That(keyError).IsNotNull();
            await Assert.That(valueError).IsNotNull();
            await Assert.That(keyError!.Message).Contains("UTF-16");
            await Assert.That(valueError!.Message).Contains("UTF-16");
            await Assert.That(keyError.Message.Contains("hidden", StringComparison.Ordinal)).IsFalse();
            await Assert.That(valueError.Message.Contains("hidden", StringComparison.Ordinal)).IsFalse();
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("__proto__")]
    [Arguments("A=B")]
    [Arguments("A\nB")]
    [Arguments("A\rB")]
    [Arguments(" K")]
    [Arguments("K ")]
    [Arguments("\tK")]
    [Arguments("K\t")]
    [Arguments("#K")]
    [Arguments("export K")]
    public async Task Format_RejectsKeysThatWouldBeChangedOrDropped(string? key)
    {
        var error = CaptureValidationError([new() { Key = key, Value = "hidden" }]);
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message.Contains("hidden", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments("Group.One")]
    [Arguments("Group-One")]
    [Arguments("123")]
    [Arguments("Ångström")]
    [Arguments("constructor")]
    [Arguments("toString")]
    [Arguments("__PROTO__")]
    [Arguments("K#middle")]
    public async Task Format_RetainsSupportedUnusualNames(string key)
    {
        await Assert.That(DotEnvEntryFormatter.Format([new() { Key = key, Value = "ok" }]))
            .IsEqualTo($"{key}=ok");
    }

    [Test]
    public async Task Format_ReportsAllValueFailuresInStableOrder()
    {
        ConfigEntryDto[] entries = [
            new() { Key = "Z", Value = "secret\r" },
            new() { Key = "A", Value = "secret'\"`#" }
        ];
        var forward = CaptureValidationError(entries);
        var reverse = CaptureValidationError(entries.Reverse());
        await Assert.That(forward).IsNotNull();
        await Assert.That(reverse).IsNotNull();
        await Assert.That(forward!.Message).IsEqualTo(reverse!.Message);
        await Assert.That(forward.Message.IndexOf("'A'", StringComparison.Ordinal)
                          < forward.Message.IndexOf("'Z'", StringComparison.Ordinal)).IsTrue();
        await Assert.That(forward.Message.Contains("secret", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Format_ValidatesKeysBeforeCollisionsAndValues_AndEscapesControls()
    {
        var error = CaptureValidationError([
            new() { Key = "Z\n\u001b", Value = "hidden\r" },
            new() { Key = "A\r", Value = "hidden" },
            new() { Key = "Group:K", Value = "hidden\r" },
            new() { Key = "Group__K", Value = "hidden" }
        ]);
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).Contains("A\\r");
        await Assert.That(error.Message).Contains("Z\\n\\u001b");
        await Assert.That(error.Message.Contains('\n')).IsFalse();
        await Assert.That(error.Message.Contains('\u001b')).IsFalse();
        await Assert.That(error.Message.Contains("duplicate", StringComparison.Ordinal)).IsFalse();
        await Assert.That(error.Message.Contains("hidden", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Format_ValidatesCollisionsBeforeValues()
    {
        var error = CaptureValidationError([
            new() { Key = "Group:K", Value = "hidden\r" },
            new() { Key = "Group__K", Value = "hidden" }
        ]);
        await Assert.That(error is DotEnvKeyCollisionException).IsTrue();
    }

    private static DotEnvExportValidationException? CaptureValidationError(IEnumerable<ConfigEntryDto> entries)
    {
        try { DotEnvEntryFormatter.Format(entries); return null; }
        catch (DotEnvExportValidationException error) { return error; }
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
