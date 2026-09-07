using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nona.Client;

public sealed partial class NonaClient
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ContentTypeHeaderName = "X-Nona-Content-Type";
    private const string LegacyContentTypeHeaderName = "ContentType";

    private async Task<NonaConfigValue> FetchConfigValueAsync(
        string path,
        CancellationToken cancellationToken)
    {
        return await SendAsync(HttpMethod.Get, path, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NonaConfigValue> SendAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        var responseBody = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            ThrowResponseException(response, request, responseBody);
        }

        var contentType = TryGetHeaderValue(response, ContentTypeHeaderName)
            ?? TryGetHeaderValue(response, LegacyContentTypeHeaderName);
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            return new NonaConfigValue
            {
                Value = responseBody ?? string.Empty,
                ContentType = NormalizeContentType(contentType!)
            };
        }

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                return DeserializeConfigValue(responseBody!);
            }
            catch (JsonException)
            {
                return new NonaConfigValue
                {
                    Value = responseBody!,
                    ContentType = InferContentType(responseBody!)
                };
            }
        }

        return new NonaConfigValue
        {
            Value = string.Empty,
            ContentType = NonaContentTypes.Text
        };
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, BuildUri(path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        ApplyApiKey(request);
        return request;
    }

    private static string? TryGetHeaderValue(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var headerValues))
        {
            return headerValues.FirstOrDefault();
        }

        if (response.Content?.Headers.TryGetValues(name, out var contentHeaderValues) == true)
        {
            return contentHeaderValues.FirstOrDefault();
        }

        return null;
    }

    private static string NormalizeContentType(string contentType)
    {
        return contentType.Trim().ToLowerInvariant() switch
        {
            "json" or "application/json" or "text/json" => NonaContentTypes.Json,
            "number" or "integer" or "float" or "double" or "decimal" => NonaContentTypes.Number,
            "boolean" or "bool" => NonaContentTypes.Boolean,
            "text" or "string" or "plain" or "text/plain" => NonaContentTypes.Text,
            _ => contentType
        };
    }

    private static string InferContentType(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.True or JsonValueKind.False => NonaContentTypes.Boolean,
                JsonValueKind.Number => NonaContentTypes.Number,
                JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null => NonaContentTypes.Json,
                _ => NonaContentTypes.Text
            };
        }
        catch (JsonException)
        {
            return NonaContentTypes.Text;
        }
    }

    private void ApplyApiKey(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Nona API calls require NonaClient.ApiKey.");
        }

        request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, ApiKey);
    }

    private Uri BuildUri(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        var baseAddress = _httpClient.BaseAddress;
        if (baseAddress is null)
        {
            throw new InvalidOperationException("NonaClient requires a BaseAddress on NonaClientOptions or HttpClient.");
        }

        return new Uri(EnsureTrailingSlash(baseAddress), path.TrimStart('/'));
    }

    private static void ThrowResponseException(
        HttpResponseMessage response,
        HttpRequestMessage request,
        string? responseBody)
    {
        var error = TryReadError(responseBody);
        var message = error.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"Nona request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
        }

        throw new NonaClientException(
            message!,
            response.StatusCode,
            request.Method.Method,
            request.RequestUri,
            responseBody,
            errorCode: error.ErrorCode,
            detail: error.Detail);
    }

    private static ResponseError TryReadError(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ResponseError.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody!);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var detail = root.TryGetProperty("detail", out var detailProperty)
                    && detailProperty.ValueKind == JsonValueKind.String
                        ? detailProperty.GetString()
                        : null;
                var errorCode = root.TryGetProperty("errorCode", out var errorCodeProperty)
                    && errorCodeProperty.ValueKind == JsonValueKind.String
                        ? errorCodeProperty.GetString()
                        : null;

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    return new ResponseError(detail, errorCode, detail);
                }

                if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                {
                    return new ResponseError(error.GetString(), errorCode, null);
                }

                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    return new ResponseError(message.GetString(), errorCode, null);
                }

                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    return new ResponseError(title.GetString(), errorCode, null);
                }
            }
        }
        catch (JsonException)
        {
            return ResponseError.Empty;
        }

        return ResponseError.Empty;
    }

    private sealed class ResponseError
    {
        public static readonly ResponseError Empty = new ResponseError(null, null, null);

        public ResponseError(string? message, string? errorCode, string? detail)
        {
            Message = message;
            ErrorCode = errorCode;
            Detail = detail;
        }

        public string? Message { get; }

        public string? ErrorCode { get; }

        public string? Detail { get; }
    }
}
