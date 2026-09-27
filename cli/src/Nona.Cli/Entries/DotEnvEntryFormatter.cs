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

        var conflicts = mappedEntries
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                $"keys {string.Join(", ", group.Select(entry => $"'{entry.Entry.Key}'"))} map to duplicate key '{group.Key}'")
            .ToList();

        if (conflicts.Count > 0)
            throw new DotEnvKeyCollisionException($"Cannot export dotenv: {string.Join("; ", conflicts)}.");

        var lines = mappedEntries
            .Select(entry => $"{entry.Key}={FormatValue(entry.Entry.Value ?? string.Empty)}");

        return string.Join('\n', lines);
    }

    private static string FormatKey(string key) => key.Replace(":", "__");

    private static string FormatValue(string value)
    {
        if (!NeedsQuoting(value))
            return value;

        if (!value.Contains('\''))
            return $"'{value}'";

        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static bool NeedsQuoting(string value) =>
        value.Length == 0
        || char.IsWhiteSpace(value[0])
        || char.IsWhiteSpace(value[^1])
        || value[0] is '"' or '\''
        || value[^1] is '"' or '\''
        || value.Contains('#')
        || value.Contains('\n')
        || value.Contains('\r');
}

internal sealed class DotEnvKeyCollisionException(string message) : Exception(message);
