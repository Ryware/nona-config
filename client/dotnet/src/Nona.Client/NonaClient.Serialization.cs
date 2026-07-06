using System.Text.Json;

namespace Nona.Client;

public sealed partial class NonaClient
{
    private static NonaConfigValue DeserializeConfigValue(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The response JSON root must be an object.");
        }

        if (!root.TryGetProperty("value", out var valueProperty) || valueProperty.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("The response JSON must include a string 'value' property.");
        }

        if (!root.TryGetProperty("contentType", out var contentTypeProperty) || contentTypeProperty.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("The response JSON must include a string 'contentType' property.");
        }

        return new NonaConfigValue
        {
            Value = valueProperty.GetString() ?? string.Empty,
            ContentType = NormalizeContentType(contentTypeProperty.GetString() ?? string.Empty)
        };
    }
}
