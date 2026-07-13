using System.Text.Json;

namespace Nona.Cli.Entries;

internal static class ConfigEntryValueRenderer
{
    internal const string LogicalContentTypeHeader = "X-Nona-Content-Type";

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };

    internal static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return "unknown";

        return contentType.Trim().ToLowerInvariant() switch
        {
            "json" or "application/json" or "text/json" => "json",
            "text" or "string" or "plain" or "text/plain" => "text",
            "number" or "integer" or "float" or "double" or "decimal" => "number",
            "boolean" or "bool" => "boolean",
            _ => "unknown"
        };
    }

    internal static void WriteValue(string value, string? contentType)
    {
        var normalizedContentType = NormalizeContentType(contentType);
        var output = normalizedContentType switch
        {
            "json" => TryFormatJson(value, out var formattedJson) ? formattedJson : value,
            "number" => IsJsonKind(value, JsonValueKind.Number) ? value.Trim() : value,
            "boolean" => IsJsonKind(value, JsonValueKind.True, JsonValueKind.False) ? value.Trim().ToLowerInvariant() : value,
            "text" => value,
            _ => TryFormatJson(value, out var formattedFallback) ? formattedFallback : value
        };

        Console.WriteLine(output);
    }

    private static bool TryFormatJson(string value, out string formatted)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            formatted = JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
            return true;
        }
        catch (JsonException)
        {
            formatted = string.Empty;
            return false;
        }
        catch (ArgumentException)
        {
            formatted = string.Empty;
            return false;
        }
    }

    private static bool IsJsonKind(string value, params JsonValueKind[] expectedKinds)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return expectedKinds.Contains(document.RootElement.ValueKind);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
