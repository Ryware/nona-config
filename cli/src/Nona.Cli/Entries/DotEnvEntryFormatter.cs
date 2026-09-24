using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries;

internal static class DotEnvEntryFormatter
{
    internal static string Format(IEnumerable<ConfigEntryDto> entries)
    {
        var lines = entries
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => $"{FormatKey(entry.Key ?? string.Empty)}={FormatValue(entry.Value ?? string.Empty)}");

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
