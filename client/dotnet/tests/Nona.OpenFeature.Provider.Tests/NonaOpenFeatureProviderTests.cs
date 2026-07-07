using System.Collections.Concurrent;
using System.Net;
using Nona.Client;
using Nona.OpenFeature.Provider;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Nona.OpenFeature.Provider.Tests;

public sealed class NonaOpenFeatureProviderTests
{
    [Fact]
    public async Task OpenFeatureProvider_ResolvesTypedValuesThroughNonaClient()
    {
        var values = new Dictionary<string, (string Value, string ContentType)>(StringComparer.Ordinal)
        {
            ["enabled"] = ("true", "boolean"),
            ["limit"] = ("42", "number"),
            ["ratio"] = ("12.5", "number"),
            ["title"] = ("Checkout", "text"),
            ["settings"] = ("""{"color":"green","enabled":true}""", "json")
        };
        var handler = new StubHttpMessageHandler(request =>
        {
            var requestUri = request.RequestUri ?? throw new InvalidOperationException("Request URI was not set.");
            var key = Uri.UnescapeDataString(requestUri.Segments.Last().TrimEnd('/'));
            Assert.True(values.TryGetValue(key, out var value), $"Unexpected flag key '{key}'.");
            return RawEntryValueResponse(value.Value, value.ContentType);
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var nona = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key"
        });
        var domain = $"nona-dotnet-{Guid.NewGuid():N}";
        await Api.Instance.SetProviderAsync(domain, new NonaOpenFeatureProvider(nona));
        var client = Api.Instance.GetClient(domain);

        Assert.True(await client.GetBooleanValueAsync("enabled", false));
        Assert.Equal(42, await client.GetIntegerValueAsync("limit", 0));
        Assert.Equal(12.5, await client.GetDoubleValueAsync("ratio", 0));
        Assert.Equal("Checkout", await client.GetStringValueAsync("title", "fallback"));

        var settings = await client.GetObjectValueAsync("settings", new Value());
        Assert.True(settings.IsStructure);
        var structure = settings.AsStructure ?? throw new InvalidOperationException("Expected structure value.");
        Assert.Equal("green", structure.GetValue("color").AsString);
        Assert.True(structure.GetValue("enabled").AsBoolean);
        Assert.All(handler.Requests, request => Assert.Equal("api-key", request.GetHeader("X-Api-Key")));
        Assert.All(handler.Requests, request => Assert.StartsWith("/api/production/", request.Uri.AbsolutePath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenFeatureProvider_ReturnsDefaultAndFlagNotFoundForMissingNonaValue()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            """{"error":"Config entry not found"}""",
            HttpStatusCode.NotFound));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var nona = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key"
        });
        var domain = $"nona-dotnet-missing-{Guid.NewGuid():N}";
        await Api.Instance.SetProviderAsync(domain, new NonaOpenFeatureProvider(nona));

        var details = await Api.Instance
            .GetClient(domain)
            .GetBooleanDetailsAsync("missing", true);

        Assert.True(details.Value);
        Assert.Equal(ErrorType.FlagNotFound, details.ErrorType);
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage RawEntryValueResponse(string value, string contentType, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(value, System.Text.Encoding.UTF8, "text/plain")
        };
        response.Headers.TryAddWithoutValidation("X-Nona-Content-Type", contentType);
        return response;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handle;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handle)
            : this(request => Task.FromResult(handle(request)))
        {
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle)
        {
            _handle = handle;
        }

        public ConcurrentQueue<CapturedRequest> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);

            Requests.Enqueue(new CapturedRequest(
                request.Method,
                request.RequestUri ?? throw new InvalidOperationException("Request URI was not set."),
                headers));

            return _handle(request);
        }
    }

    private sealed class CapturedRequest
    {
        public CapturedRequest(
            HttpMethod method,
            Uri uri,
            IReadOnlyDictionary<string, string[]> headers)
        {
            Method = method;
            Uri = uri;
            Headers = headers;
        }

        public HttpMethod Method { get; }

        public Uri Uri { get; }

        public IReadOnlyDictionary<string, string[]> Headers { get; }

        public string? GetHeader(string name)
        {
            return Headers.TryGetValue(name, out var values) ? values.SingleOrDefault() : null;
        }
    }
}
