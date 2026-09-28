using System.Globalization;
using System.Text;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries;

internal static class DotEnvEntryFormatter
{
    internal static string Format(IEnumerable<ConfigEntryDto> entries)
    {
        var mappedEntries = entries
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new { Entry = entry, Key = FormatKey(entry.Key ?? string.Empty) })
            .ToList();

        var invalidKeys = mappedEntries
            .Select(entry => (entry.Entry.Key, Error: GetKeyError(entry.Key)))
            .Where(entry => entry.Error is not null)
            .Select(entry => $"key {DisplayKey(entry.Key)}: {entry.Error}")
            .ToList();
        ThrowIfInvalid(invalidKeys);

        var conflicts = mappedEntries
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                $"keys {string.Join(", ", group.Select(entry => DisplayKey(entry.Entry.Key)))} map to duplicate key {DisplayKey(group.Key)}")
            .ToList();

        if (conflicts.Count > 0)
            throw new DotEnvKeyCollisionException($"Cannot export dotenv: {string.Join("; ", conflicts)}.");

        var lines = new List<string>(mappedEntries.Count);
        var invalidValues = new List<string>();
        foreach (var entry in mappedEntries)
        {
            var (value, error) = FormatValue(entry.Entry.Value ?? string.Empty);
            if (error is not null)
                invalidValues.Add($"key {DisplayKey(entry.Entry.Key)}: {error}");
            else
                lines.Add($"{entry.Key}={value}");
        }
        ThrowIfInvalid(invalidValues);

        return string.Join('\n', lines);
    }

    private static string FormatKey(string key) => key.Replace(":", "__");

    private static (string? Value, string? Error) FormatValue(string value)
    {
        if (!IsValidUtf16(value))
            return (null, "value contains invalid UTF-16");
        if (value.Contains('\r'))
            return (null, "value contains a carriage return that util.parseEnv removes");
        if (value.Length == 0)
            return ("''", null);
        if (CanWriteUnquoted(value))
            return (value, null);

        if (!value.Contains('\''))
            return ($"'{value}'", null);
        if (!value.Contains('`'))
            return ($"`{value}`", null);
        // Node expands literal backslash+n only inside double quotes. It does
        // not unescape backslashes or escaped quote delimiters.
        if (!value.Contains('"') && !value.Contains("\\n", StringComparison.Ordinal))
            return ($"\"{value}\"", null);

        return (null, "value has no quote delimiter that util.parseEnv can preserve");
    }

    private static bool CanWriteUnquoted(string value) =>
        value[0] is not ('\'' or '"' or '`' or ' ' or '\t')
        && value[^1] is not (' ' or '\t')
        && !value.Contains('#')
        && !value.Contains('\n');

    private static string? GetKeyError(string key)
    {
        if (key.Length == 0) return "key is empty";
        if (!IsValidUtf16(key)) return "key contains invalid UTF-16";
        if (key.Contains('\r') || key.Contains('\n')) return "key contains a carriage return or line feed";
        if (key.Contains('=')) return "key contains '='";
        if (key[0] is ' ' or '\t' || key[^1] is ' ' or '\t') return "key has leading or trailing spaces or tabs";
        if (key.StartsWith('#')) return "key starts with a comment marker";
        if (key.StartsWith("export ", StringComparison.Ordinal)) return "key starts with the export keyword";
        if (key == "__proto__") return "key is dropped by util.parseEnv";
        return null;
    }

    private static bool IsValidUtf16(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (++i == text.Length || !char.IsLowSurrogate(text[i])) return false;
            }
            else if (char.IsLowSurrogate(text[i])) return false;
        }
        return true;
    }

    private static void ThrowIfInvalid(List<string> errors)
    {
        if (errors.Count > 0)
            throw new DotEnvExportValidationException($"Cannot export dotenv: {string.Join("; ", errors)}.");
    }

    private static string DisplayKey(string? key)
    {
        if (key is null) return "<null>";
        var result = new StringBuilder("'");
        foreach (var c in key)
        {
            result.Append(c switch
            {
                '\\' => "\\\\",
                '\'' => "\\'",
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                _ when char.IsControl(c) || char.IsSurrogate(c)
                       || char.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
                    => $"\\u{(int)c:x4}",
                _ => c.ToString()
            });
        }
        return result.Append('\'').ToString();
    }
}

internal class DotEnvExportValidationException(string message) : Exception(message);

internal sealed class DotEnvKeyCollisionException(string message) : DotEnvExportValidationException(message);
