using System.Text.Json;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Releases;

internal sealed class ReleaseEditException(string message) : Exception(message);

internal static class ReleaseEntryEditing
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static List<ConfigReleaseEntryDto> Clone(
        IEnumerable<ConfigReleaseEntryDto> entries)
        => entries.Select(Clone).ToList();

    public static List<ConfigReleaseEntryDto> ApplyDirectEdits(
        IEnumerable<ConfigReleaseEntryDto> sourceEntries,
        IReadOnlyList<string> setValues,
        IReadOnlyList<string> deleteKeys)
    {
        var entries = Clone(sourceEntries);
        var parsedSets = ParseSets(setValues);
        var parsedDeletes = ParseDeletes(deleteKeys);
        var setKeys = parsedSets
            .Select(edit => edit.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflict = parsedDeletes.FirstOrDefault(setKeys.Contains);
        if (conflict is not null)
        {
            throw new ReleaseEditException(
                $"Key '{conflict}' cannot be used with both --set and --delete.");
        }

        foreach (var edit in parsedSets)
        {
            var existing = entries.FirstOrDefault(
                entry => string.Equals(entry.Key, edit.Key, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Value = edit.Value;
                continue;
            }

            entries.Add(new ConfigReleaseEntryDto
            {
                Key = edit.Key,
                Value = edit.Value,
                ContentType = InferContentType(edit.Value),
                Scope = "all"
            });
        }

        foreach (var key in parsedDeletes)
        {
            var removed = entries.RemoveAll(
                entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
                throw new ReleaseEditException($"Cannot delete unknown release entry '{key}'.");
        }

        return entries;
    }

    public static async Task<List<ConfigReleaseEntryDto>> ReadFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ReleaseEditException("--from-file requires a file path.");

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
        {
            throw new ReleaseEditException(
                $"Could not read release entries file '{path}': {exception.Message}");
        }

        return ParseJson(json, path);
    }

    public static List<ConfigReleaseEntryDto> ParseJson(
        string json,
        string sourceDescription)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ReleaseEditException(
                $"Release entries in '{sourceDescription}' are not valid JSON: {exception.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new ReleaseEditException(
                    $"Release entries in '{sourceDescription}' must be a JSON array.");
            }

            var entries = new List<ConfigReleaseEntryDto>();
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Null)
                {
                    throw new ReleaseEditException(
                        $"Release entry at index {index} in '{sourceDescription}' cannot be null.");
                }

                ReleaseEntryDocument? entry;
                try
                {
                    entry = element.Deserialize<ReleaseEntryDocument>(JsonOptions);
                }
                catch (JsonException exception)
                {
                    throw new ReleaseEditException(
                        $"Release entry at index {index} in '{sourceDescription}' is invalid: " +
                        exception.Message);
                }

                if (entry?.Key is null ||
                    entry.Value is null ||
                    entry.ContentType is null ||
                    entry.Scope is null)
                {
                    throw new ReleaseEditException(
                        $"Release entry at index {index} in '{sourceDescription}' must contain " +
                        "key, value, contentType, and scope.");
                }

                entries.Add(new ConfigReleaseEntryDto
                {
                    Key = entry.Key,
                    Value = entry.Value,
                    ContentType = entry.ContentType,
                    Scope = entry.Scope
                });
                index++;
            }

            return entries;
        }
    }

    private static List<ReleaseSetEdit> ParseSets(IReadOnlyList<string> values)
    {
        var edits = new List<ReleaseSetEdit>(values.Count);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var separator = value.IndexOf('=');
            if (separator <= 0)
            {
                throw new ReleaseEditException(
                    $"Invalid --set value '{value}'. Use key=value.");
            }

            var key = value[..separator].Trim();
            if (key.Length == 0)
                throw new ReleaseEditException("--set requires a non-empty key before '='.");
            if (!keys.Add(key))
                throw new ReleaseEditException($"Duplicate --set for key '{key}'.");

            edits.Add(new ReleaseSetEdit(key, value[(separator + 1)..]));
        }

        return edits;
    }

    private static List<string> ParseDeletes(IReadOnlyList<string> values)
    {
        var keys = new List<string>(values.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var key = value.Trim();
            if (key.Length == 0)
                throw new ReleaseEditException("--delete requires a non-empty key.");
            if (!seen.Add(key))
                throw new ReleaseEditException($"Duplicate --delete for key '{key}'.");

            keys.Add(key);
        }

        return keys;
    }

    private static ConfigReleaseEntryDto Clone(ConfigReleaseEntryDto entry)
        => new()
        {
            Key = entry.Key,
            Value = entry.Value,
            ContentType = entry.ContentType,
            Scope = entry.Scope
        };

    private static string InferContentType(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Number => "number",
                JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null => "json",
                _ => "text"
            };
        }
        catch (JsonException)
        {
            return "text";
        }
        catch (ArgumentException)
        {
            return "text";
        }
    }

    private sealed record ReleaseSetEdit(string Key, string Value);

    private sealed record ReleaseEntryDocument(
        string? Key,
        string? Value,
        string? ContentType,
        string? Scope);
}
