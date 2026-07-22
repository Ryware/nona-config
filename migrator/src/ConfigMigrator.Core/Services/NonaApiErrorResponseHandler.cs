using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Nona.Migrator.Core.Services;

public sealed class NonaApiErrorResponseHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var statusCode = (int)response.StatusCode;
        if (statusCode is < 400 or > 599)
            return response;

        var body = await ReadBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
        var error = ParseError(body);
        var fallback = FallbackMessage(response.StatusCode, response.ReasonPhrase);
        var message = error.Message ?? PlainTextMessage(response.Content, body) ?? fallback;

        var normalized = JsonSerializer.Serialize(new ProblemEnvelope(
            Type: error.Type ?? TypeFor(statusCode),
            Title: error.Title ?? fallback,
            Status: statusCode,
            Detail: message,
            Instance: error.Instance,
            Errors: error.Errors,
            ErrorCode: error.ErrorCode));

        response.Content?.Dispose();
        response.Content = new StringContent(normalized, Encoding.UTF8, "application/problem+json");
        return response;
    }

    private static async Task<string?> ReadBodyAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
            return null;

        try
        {
            return await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DecoderFallbackException)
        {
            return null;
        }
    }

    private static ParsedError ParseError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return default;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return default;

            var root = document.RootElement;
            var error = GetString(root, "error");
            var detail = GetString(root, "detail");
            var title = GetString(root, "title");
            return new ParsedError(
                Message: error ?? detail ?? title,
                ErrorCode: GetString(root, "errorCode"),
                Title: title,
                Type: GetString(root, "type"),
                Instance: GetString(root, "instance"),
                Errors: GetErrors(root));
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? PlainTextMessage(HttpContent? content, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var mediaType = content?.Headers.ContentType?.MediaType;
        return mediaType?.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) == true
            ? body
            : null;
    }

    private static string? GetString(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string[]>? GetErrors(JsonElement root)
    {
        var errorsProperty = root.EnumerateObject().FirstOrDefault(property =>
            property.Name.Equals("errors", StringComparison.OrdinalIgnoreCase));
        if (errorsProperty.Value.ValueKind != JsonValueKind.Object)
            return null;

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var field in errorsProperty.Value.EnumerateObject())
        {
            var messages = field.Value.ValueKind switch
            {
                JsonValueKind.Array => field.Value
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .ToArray(),
                JsonValueKind.String => [field.Value.GetString()!],
                _ => []
            };

            errors[field.Name] = messages;
        }

        return errors;
    }

    private static string FallbackMessage(HttpStatusCode statusCode, string? reasonPhrase)
    {
        if (!string.IsNullOrWhiteSpace(reasonPhrase))
            return reasonPhrase;

        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.Conflict => "Conflict",
            >= HttpStatusCode.InternalServerError => "Server error",
            _ => "The server rejected the request"
        };
    }

    private static string TypeFor(int statusCode)
        => $"https://tools.ietf.org/html/rfc9110#section-{statusCode switch
        {
            400 => "15.5.1",
            401 => "15.5.2",
            403 => "15.5.4",
            404 => "15.5.5",
            409 => "15.5.10",
            410 => "15.5.11",
            >= 500 => "15.6.1",
            _ => "15.5.1"
        }}";

    private readonly record struct ParsedError(
        string? Message,
        string? ErrorCode,
        string? Title,
        string? Type,
        string? Instance,
        IReadOnlyDictionary<string, string[]>? Errors);

    private sealed record ProblemEnvelope(
        [property: System.Text.Json.Serialization.JsonPropertyName("type")] string Type,
        [property: System.Text.Json.Serialization.JsonPropertyName("title")] string Title,
        [property: System.Text.Json.Serialization.JsonPropertyName("status")] int Status,
        [property: System.Text.Json.Serialization.JsonPropertyName("detail")] string Detail,
        [property: System.Text.Json.Serialization.JsonPropertyName("instance")]
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        string? Instance,
        [property: System.Text.Json.Serialization.JsonPropertyName("errors")]
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyDictionary<string, string[]>? Errors,
        [property: System.Text.Json.Serialization.JsonPropertyName("errorCode")]
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        string? ErrorCode);
}

public static class NonaApiHttpClientFactory
{
    public static HttpClient Create()
    {
        var handlers = KiotaClientFactory.CreateDefaultHandlers();
        handlers.Insert(0, new NonaApiErrorResponseHandler());
        return KiotaClientFactory.Create(handlers, KiotaClientFactory.GetDefaultHttpMessageHandler());
    }
}
