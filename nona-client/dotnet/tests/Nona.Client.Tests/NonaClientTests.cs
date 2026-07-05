using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Serialization;
using Nona.Client;

namespace Nona.Client.Tests;

public sealed class NonaClientTests
{
    [Fact]
    public async Task GetConfigValueAsync_SendsApiKeyAndParsesValue()
    {
        var handler = new StubHttpMessageHandler(_ => RawEntryValueResponse("enabled", "text"));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key"
        });

        var value = await client.GetConfigValueAsync("production", "Features:Checkout");

        Assert.Equal("enabled", value.Value);
        Assert.Equal("text", value.ContentType);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://nona.test/api/production/Features%3ACheckout", request.Uri.AbsoluteUri);
        Assert.Equal("api-key", request.GetHeader("X-Api-Key"));
    }

    [Fact]
    public async Task TryGetConfigValueAsync_ReturnsNullForNotFound()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            """{"error":"Config entry not found"}""",
            HttpStatusCode.NotFound));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key"
        });

        var value = await client.TryGetConfigValueAsync("production", "missing");

        Assert.Null(value);
    }

    [Fact]
    public async Task GetStringValueAsync_ReturnsRawConfigValue()
    {
        var handler = new StubHttpMessageHandler(_ => RawEntryValueResponse("enabled", "text"));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key"
        });

        Assert.Equal("enabled", await client.GetStringValueAsync("production", "flag"));
    }

    [Fact]
    public async Task GetConfigValueAsync_ReturnsFreshCachedValueWithoutSecondRequest()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return RawEntryValueResponse($"value-{requestCount}", "text");
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMinutes(1)
        });

        var first = await client.GetConfigValueAsync("production", "flag");
        var second = await client.GetConfigValueAsync("production", "flag");

        Assert.Equal("value-1", first.Value);
        Assert.Equal("value-1", second.Value);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetConfigValueAsync_DeduplicatesConcurrentRequestsForSameKey()
    {
        var releaseResponse = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(async _ =>
        {
            Interlocked.Increment(ref requestCount);
            await releaseResponse.Task;
            return RawEntryValueResponse("enabled", "text");
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMinutes(1)
        });

        var startRequests = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                await startRequests.Task;
                return await client.GetConfigValueAsync("production", "flag");
            }))
            .ToArray();

        startRequests.SetResult(true);
        await WaitForAsync(() => Volatile.Read(ref requestCount) == 1);
        await Task.Delay(50);

        Assert.Equal(1, Volatile.Read(ref requestCount));

        releaseResponse.SetResult(true);
        var values = await Task.WhenAll(requests);

        Assert.All(values, value =>
        {
            Assert.Equal("enabled", value.Value);
            Assert.Equal("text", value.ContentType);
        });
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetConfigValueAsync_DoesNotDeduplicateDifferentKeys()
    {
        var releaseResponses = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async request =>
        {
            await releaseResponses.Task;

            var key = request.RequestUri?.Segments.Last().TrimEnd('/');
            return RawEntryValueResponse(key ?? string.Empty, "text");
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMinutes(1)
        });

        var requests = new[]
        {
            client.GetConfigValueAsync("production", "one"),
            client.GetConfigValueAsync("production", "two"),
            client.GetConfigValueAsync("production", "three")
        };

        await WaitForAsync(() => handler.Requests.Count == 3);
        releaseResponses.SetResult(true);

        var values = await Task.WhenAll(requests);

        Assert.Equal(new[] { "one", "two", "three" }, values.Select(value => value.Value).ToArray());
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task GetConfigValueAsync_RefreshesExpiredCacheWhenStaleCacheIsDisabled()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return RawEntryValueResponse($"value-{requestCount}", "text");
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMilliseconds(25)
        });

        Assert.Equal("value-1", (await client.GetConfigValueAsync("production", "flag")).Value);
        await Task.Delay(80);

        Assert.Equal("value-2", (await client.GetConfigValueAsync("production", "flag")).Value);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetConfigValueAsync_CanServeStaleCacheAndRefreshInBackground()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return RawEntryValueResponse($"value-{requestCount}", "text");
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMilliseconds(25),
            AllowStaleCache = true
        });

        Assert.Equal("value-1", (await client.GetConfigValueAsync("production", "flag")).Value);
        await Task.Delay(80);

        var stale = await client.GetConfigValueAsync("production", "flag");
        Assert.Equal("value-1", stale.Value);

        await WaitForAsync(() => handler.Requests.Count >= 2);
        Assert.Equal("value-2", (await client.GetConfigValueAsync("production", "flag")).Value);
    }

    [Fact]
    public async Task GetConfigValueAsync_EvictsLeastRecentlyUsedEntriesWhenMemoryLimitIsReached()
    {
        var largeValue = new string('x', 600_000);
        var handler = new StubHttpMessageHandler(request =>
        {
            var key = request.RequestUri?.Segments.Last().TrimEnd('/');
            return RawEntryValueResponse($"{key}-{largeValue}", "text");
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMinutes(1),
            CacheMemoryLimitMegabytes = 1
        });

        Assert.StartsWith("one-", (await client.GetConfigValueAsync("production", "one")).Value);
        Assert.StartsWith("two-", (await client.GetConfigValueAsync("production", "two")).Value);
        Assert.StartsWith("one-", (await client.GetConfigValueAsync("production", "one")).Value);

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task GetJsonValueAsync_DeserializesConfigValue()
    {
        var handler = new StubHttpMessageHandler(_ => RawEntryValueResponse("""{"enabled":true}""", "json"));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key"
        });

        var value = await client.GetJsonValueAsync(
            "production",
            "settings",
            NonaClientTestsJsonContext.Default.JsonFlag);

        Assert.NotNull(value);
        Assert.True(value.Enabled);
    }

    [Fact]
    public async Task GetConfigValueAsync_CanReadLegacyJsonResponse()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {"value":"enabled","contentType":"text"}
            """));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key"
        });

        var value = await client.GetConfigValueAsync("production", "flag");

        Assert.Equal("enabled", value.Value);
        Assert.Equal("text", value.ContentType);
    }

    [Fact]
    public async Task GetConfigValueAsync_AllowsEmptyRawValue()
    {
        var handler = new StubHttpMessageHandler(_ => RawEntryValueResponse(string.Empty, "text"));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key"
        });

        var value = await client.GetConfigValueAsync("production", "empty");

        Assert.Equal(string.Empty, value.Value);
        Assert.Equal("text", value.ContentType);
    }

    [Fact]
    public async Task FailedRequest_ThrowsNonaClientExceptionWithServerError()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            """{"error":"Config entry not found"}""",
            HttpStatusCode.NotFound));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key"
        });

        var ex = await Assert.ThrowsAsync<NonaClientException>(() =>
            client.GetConfigValueAsync("production", "missing"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Config entry not found", ex.Message);
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

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "Timed out waiting for condition.");
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

    internal sealed class JsonFlag
    {
        public bool Enabled { get; set; }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NonaClientTests.JsonFlag))]
internal sealed partial class NonaClientTestsJsonContext : JsonSerializerContext
{
}
