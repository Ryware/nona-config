using System.Diagnostics;
using System.Text.Json;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Releases;

internal sealed class ReleaseEditException(string message) : Exception(message);

internal static class ReleaseEntryEditing
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
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

    public static string ToJson(IEnumerable<ConfigReleaseEntryDto> entries)
        => JsonSerializer.Serialize(
            entries.Select(entry => new ReleaseEntryDocument(
                entry.Key,
                entry.Value,
                entry.ContentType,
                entry.Scope)),
            JsonOptions);

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

internal interface IReleaseEntryEditor
{
    Task<List<ConfigReleaseEntryDto>> EditAsync(
        IReadOnlyList<ConfigReleaseEntryDto> entries,
        CancellationToken cancellationToken);
}

internal sealed class ReleaseEntryEditor(
    Func<string, string?>? environmentVariable = null,
    Func<string, string, CancellationToken, Task<int>>? launcher = null)
    : IReleaseEntryEditor
{
    private readonly Func<string, string?> _environmentVariable =
        environmentVariable ?? Environment.GetEnvironmentVariable;
    private readonly Func<string, string, CancellationToken, Task<int>> _launcher =
        launcher ?? LaunchAsync;

    public async Task<List<ConfigReleaseEntryDto>> EditAsync(
        IReadOnlyList<ConfigReleaseEntryDto> entries,
        CancellationToken cancellationToken)
    {
        var editor = FirstNonEmpty(
            _environmentVariable("VISUAL"),
            _environmentVariable("EDITOR"));
        if (editor is null)
        {
            throw new ReleaseEditException(
                "No editor is configured. Set VISUAL or EDITOR, or use --set, --delete, " +
                "or --from-file.");
        }

        var path = Path.Combine(
            Path.GetTempPath(),
            $"nona-release-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(
                path,
                ReleaseEntryEditing.ToJson(entries),
                cancellationToken);

            var exitCode = await _launcher(editor, path, cancellationToken);
            if (exitCode != 0)
            {
                throw new ReleaseEditException(
                    $"Editor exited with code {exitCode}; release was not published.");
            }

            return await ReleaseEntryEditing.ReadFileAsync(path, cancellationToken);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // The edit result still determines the command outcome.
            }
            catch (UnauthorizedAccessException)
            {
                // The edit result still determines the command outcome.
            }
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static async Task<int> LaunchAsync(
        string editor,
        string path,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
            startInfo.ArgumentList.Add("/D");
            startInfo.ArgumentList.Add("/S");
            startInfo.ArgumentList.Add("/C");
            startInfo.ArgumentList.Add($"{editor} \"{path.Replace("\"", "\"\"")}\"");
        }
        else
        {
            startInfo.FileName = "/bin/sh";
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add($"exec {editor} \"$1\"");
            startInfo.ArgumentList.Add("nona-release-editor");
            startInfo.ArgumentList.Add(path);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                throw new ReleaseEditException($"Could not start editor '{editor}'.");

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new ReleaseEditException(
                $"Could not start editor '{editor}': {exception.Message}");
        }
    }
}
