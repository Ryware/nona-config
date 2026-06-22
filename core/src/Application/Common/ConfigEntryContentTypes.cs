using System.Text.Json;

namespace Nona.Application.Common;

public static class ConfigEntryContentTypes
{
    public const string Json = "json";
    public const string Text = "text";
    public const string Number = "number";
    public const string Boolean = "boolean";

    public static readonly string[] LogicalTypes = [Json, Text, Number, Boolean];

    public static string Resolve(string? contentType, string value)
        => Normalize(contentType) ?? Infer(value);

    public static string? Normalize(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        return contentType.Trim().ToLowerInvariant() switch
        {
            Json or "application/json" or "text/json" => Json,
            Text or "string" or "plain" or "text/plain" => Text,
            Number or "integer" or "float" or "double" or "decimal" => Number,
            Boolean or "bool" => Boolean,
            _ => null
        };
    }

    public static string Infer(string value)
    {
        var kind = TryGetJsonValueKind(value);

        return kind switch
        {
            JsonValueKind.True or JsonValueKind.False => Boolean,
            JsonValueKind.Number => Number,
            JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null => Json,
            _ => Text
        };
    }

    public static bool IsValidValue(string value, string contentType, out string? error)
    {
        error = null;

        switch (Normalize(contentType))
        {
            case Json:
                if (TryGetJsonValueKind(value) is not null)
                    return true;

                error = "Value must be valid JSON when contentType is 'json'.";
                return false;

            case Number:
                if (TryGetJsonValueKind(value) == JsonValueKind.Number)
                    return true;

                error = "Value must be a valid JSON number when contentType is 'number'.";
                return false;

            case Boolean:
                if (TryGetJsonValueKind(value) is JsonValueKind.True or JsonValueKind.False)
                    return true;

                error = "Value must be 'true' or 'false' when contentType is 'boolean'.";
                return false;

            case Text:
                return true;

            default:
                error = $"Content type must be one of: {string.Join(", ", LogicalTypes)}.";
                return false;
        }
    }

    private static JsonValueKind? TryGetJsonValueKind(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
