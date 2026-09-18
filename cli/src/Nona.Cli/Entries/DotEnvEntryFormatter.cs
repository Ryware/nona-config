using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries;

internal static class DotEnvEntryFormatter
{
    internal static string Format(IEnumerable<ConfigEntryDto> entries)
    {
        var lines = entries
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => $"{entry.Key}=\"{EscapeValue(entry.Value ?? string.Empty)}\"");

        return string.Join('\n', lines);
    }

    private static string EscapeValue(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
