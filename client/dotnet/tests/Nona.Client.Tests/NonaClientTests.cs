using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Serialization;
using Nona.Client;

namespace Nona.Client.Tests;

public sealed class NonaClientTests
{
    [Fact]
    public async Task GetAllValuesAsync_UsesPrefixEtagAcrossCaseVariantsAndReturnsDefensiveCopies()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return requestCount == 1
                ? BulkValuesResponse(
                    """{"GroupA:One":{"value":"1","contentType":"number"}}""",
                    "\"group-a\"")
                : new HttpResponseMessage(HttpStatusCode.NotModified);
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://nona.test/") };
        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        var first = await client.GetAllValuesAsync("GroupA:");
        first["GroupA:One"].Value = "caller-mutated";
        var second = await client.GetAllValuesAsync("groupa:");

        Assert.Equal("1", second["GroupA:One"].Value);
        Assert.Equal(2, handler.Requests.Count);
        var requests = handler.Requests.ToArray();
        Assert.Equal("https://nona.test/api/production?prefix=GroupA%3A", requests[0].Uri.AbsoluteUri);
        Assert.Equal("https://nona.test/api/production?prefix=groupa%3A", requests[1].Uri.AbsoluteUri);
        Assert.Equal("\"group-a\"", requests[1].GetHeader("If-None-Match"));
    }

    [Fact]
    public async Task GetAllValuesForReleaseAsync_CombinesReleaseAndPrefixSelectors()
    {
        var handler = new StubHttpMessageHandler(_ => BulkValuesResponse("{}", "\"empty\""));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://nona.test/") };
        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key",
            ReleaseVersion = "1.0.0"
        });

        await client.GetAllValuesForReleaseAsync("2.3.4", "GroupA:");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "https://nona.test/api/production?version=2.3.4&prefix=GroupA%3A",
            request.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task GetAllValuesAsync_DeduplicatesCaseInsensitivePrefixes()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(_ => pending.Task);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://nona.test/") };
        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        var first = client.GetAllValuesAsync("GroupA:");
        var second = client.GetAllValuesAsync("groupa:");
        Assert.Single(handler.Requests);

        pending.SetResult(BulkValuesResponse(
            """{"GroupA:One":{"value":"1","contentType":"number"}}""",
            "\"group-a\""));
        var results = await Task.WhenAll(first, second);

        Assert.Equal("1", results[0]["GroupA:One"].Value);
        Assert.Equal("1", results[1]["GroupA:One"].Value);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PrefixedBulkRead_PrimesOnlyReturnedSingleKeyValues()
    {
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.Query.Contains("prefix=", StringComparison.Ordinal)
                ? BulkValuesResponse(
                    """{"GroupA:One":{"value":"1","contentType":"number"}}""",
                    "\"group-a\"")
                : RawEntryValueResponse("2", "number"));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://nona.test/") };
        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        await client.GetAllValuesAsync("GroupA:");
        var groupA = await client.GetConfigValueAsync("GroupA:One");
        var groupB = await client.GetConfigValueAsync("GroupB:One");

        Assert.Equal("1", groupA.Value);
        Assert.Equal("2", groupB.Value);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task BulkSnapshots_ShareMemoryLimitAndEvictLeastRecentlyUsedPrefix()
    {
        var largeValue = new string('x', 350_000);
        var handler = new StubHttpMessageHandler(request =>
        {
            var prefix = Uri.UnescapeDataString(request.RequestUri!.Query.Split('=')[1]);
            if (request.Headers.Contains("If-None-Match"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotModified);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                [$"{prefix}One"] = new { value = largeValue, contentType = "text" }
            });
            return BulkValuesResponse(
                json,
                $"\"{prefix}\"");
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://nona.test/") };
        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key",
            CacheMemoryLimitMegabytes = 1
        });

        await client.GetAllValuesAsync("GroupA:");
        await client.GetAllValuesAsync("GroupB:");
        await client.GetAllValuesAsync("GroupB:");
        await client.GetAllValuesAsync("GroupA:");

        var requests = handler.Requests.ToArray();
        Assert.Equal(4, requests.Length);
        Assert.Equal("\"GroupB:\"", requests[2].GetHeader("If-None-Match"));
        Assert.Null(requests[3].GetHeader("If-None-Match"));
    }

    [Fact]
    public async Task GetAllValuesAsync_CallerCancellationDoesNotCancelSharedFetch()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(_ => pending.Task);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://nona.test/") };
        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key"
        });
        using var cancellation = new CancellationTokenSource();

        var canceledCaller = client.GetAllValuesAsync("GroupA:", cancellation.Token);
        var successfulCaller = client.GetAllValuesAsync("groupa:");
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledCaller);
        pending.SetResult(BulkValuesResponse(
            """{"GroupA:One":{"value":"1","contentType":"number"}}""",
            "\"group-a\""));
        var values = await successfulCaller;

        Assert.Equal("1", values["GroupA:One"].Value);
        Assert.Single(handler.Requests);
    }

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
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        Assert.Equal("production", client.EnvironmentId);

        var value = await client.GetConfigValueAsync("Features:Checkout");

        Assert.Equal("enabled", value.Value);
        Assert.Equal("text", value.ContentType);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://nona.test/api/production/Features%3ACheckout", request.Uri.AbsoluteUri);
        Assert.Equal("api-key", request.GetHeader("X-Api-Key"));
    }

    [Fact]
    public async Task GetConfigValueAsync_SendsConfiguredReleaseVersion()
    {
        var handler = new StubHttpMessageHandler(_ => RawEntryValueResponse("enabled", "text"));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key",
            ReleaseVersion = "1.1.x"
        });

        await client.GetConfigValueAsync("Features:Checkout");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("1.1.x", client.ReleaseVersion);
        Assert.Equal("https://nona.test/api/production/Features%3ACheckout?version=1.1.x", request.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task GetConfigValueForReleaseAsync_RequestReleaseVersionOverridesConfiguredReleaseVersion()
    {
        var handler = new StubHttpMessageHandler(_ => RawEntryValueResponse("enabled", "text"));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key",
            ReleaseVersion = "1.1.x"
        });

        await client.GetConfigValueForReleaseAsync("Features:Checkout", "1.1.0");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://nona.test/api/production/Features%3ACheckout?version=1.1.0", request.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task TryGetConfigValueForReleaseAsync_ReturnsNullAndUsesRequestedReleaseVersion()
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
            EnvironmentId = "production",
            ApiKey = "api-key",
            ReleaseVersion = "1.1.x"
        });

        var value = await client.TryGetConfigValueForReleaseAsync("missing", "1.1.0");

        Assert.Null(value);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://nona.test/api/production/missing?version=1.1.0", request.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task GetStringValueForReleaseAsync_ReturnsRawValueAndUsesRequestedReleaseVersion()
    {
        var handler = new StubHttpMessageHandler(_ => RawEntryValueResponse("enabled", "text"));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key",
            ReleaseVersion = "1.1.x"
        });

        var value = await client.GetStringValueForReleaseAsync("Features:Checkout", "1.1.0");

        Assert.Equal("enabled", value);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://nona.test/api/production/Features%3ACheckout?version=1.1.0", request.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task GetJsonValueForReleaseAsync_DeserializesValueAndUsesRequestedReleaseVersion()
    {
        var handler = new StubHttpMessageHandler(_ => RawEntryValueResponse("""{"enabled":true}""", "json"));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key",
            ReleaseVersion = "1.1.x"
        });

        var value = await client.GetJsonValueForReleaseAsync(
            "Features:Checkout",
            NonaClientTestsJsonContext.Default.JsonFlag,
            "1.1.0");

        Assert.NotNull(value);
        Assert.True(value.Enabled);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://nona.test/api/production/Features%3ACheckout?version=1.1.0", request.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task GetConfigValueAsync_UsesApiKeyCapturedAtConstruction()
    {
        var handler = new StubHttpMessageHandler(_ => RawEntryValueResponse("enabled", "text"));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        var options = new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "original-key"
        };

        using var client = new NonaClient(httpClient, options);
        options.ApiKey = "changed-key";

        await client.GetConfigValueAsync("flag");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("original-key", client.ApiKey);
        Assert.Equal("original-key", request.GetHeader("X-Api-Key"));
    }

    [Fact]
    public void Constructor_RequiresEnvironmentId()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        var ex = Assert.Throws<ArgumentException>(() => new NonaClient(httpClient, new NonaClientOptions
        {
            ApiKey = "api-key"
        }));

        Assert.Equal(nameof(NonaClientOptions.EnvironmentId), ex.ParamName);
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
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        var value = await client.TryGetConfigValueAsync("missing");

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
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        Assert.Equal("enabled", await client.GetStringValueAsync("flag"));
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
            EnvironmentId = "production",
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMinutes(1)
        });

        var first = await client.GetConfigValueAsync("flag");
        var second = await client.GetConfigValueAsync("flag");

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
            EnvironmentId = "production",
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMinutes(1)
        });

        const int callerCount = 10;
        var readyCallers = 0;
        var invokedCallers = 0;
        var startRequests = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, callerCount)
            .Select(_ => Task.Run(async () =>
            {
                Interlocked.Increment(ref readyCallers);
                await startRequests.Task;
                var request = client.GetConfigValueAsync("flag");
                Interlocked.Increment(ref invokedCallers);
                return await request;
            }))
            .ToArray();

        await WaitForAsync(() => Volatile.Read(ref readyCallers) == callerCount);
        startRequests.SetResult(true);
        await WaitForAsync(() => Volatile.Read(ref invokedCallers) == callerCount);

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
            EnvironmentId = "production",
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMinutes(1)
        });

        var requests = new[]
        {
            client.GetConfigValueAsync("one"),
            client.GetConfigValueAsync("two"),
            client.GetConfigValueAsync("three")
        };

        await WaitForAsync(() => handler.Requests.Count == 3);
        releaseResponses.SetResult(true);

        var values = await Task.WhenAll(requests);

        Assert.Equal(new[] { "one", "two", "three" }, values.Select(value => value.Value).ToArray());
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task GetConfigValueForReleaseAsync_DoesNotDeduplicateDifferentReleaseVersions()
    {
        var releaseResponses = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async request =>
        {
            await releaseResponses.Task;
            return RawEntryValueResponse(request.RequestUri?.Query ?? string.Empty, "text");
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMinutes(1)
        });

        var requests = new[]
        {
            client.GetConfigValueForReleaseAsync("flag", "1.1.0"),
            client.GetConfigValueForReleaseAsync("flag", "1.1.1")
        };

        await WaitForAsync(() => handler.Requests.Count == 2);
        releaseResponses.SetResult(true);

        var values = await Task.WhenAll(requests);

        Assert.Equal(new[] { "?version=1.1.0", "?version=1.1.1" }, values.Select(value => value.Value).ToArray());
        Assert.Equal(2, handler.Requests.Count);
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
            EnvironmentId = "production",
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMilliseconds(25)
        });

        Assert.Equal("value-1", (await client.GetConfigValueAsync("flag")).Value);
        await Task.Delay(80);

        Assert.Equal("value-2", (await client.GetConfigValueAsync("flag")).Value);
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
            EnvironmentId = "production",
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMilliseconds(25),
            AllowStaleCache = true
        });

        Assert.Equal("value-1", (await client.GetConfigValueAsync("flag")).Value);
        await Task.Delay(80);

        var stale = await client.GetConfigValueAsync("flag");
        Assert.Equal("value-1", stale.Value);

        await WaitForAsync(async () =>
            (await client.GetConfigValueAsync("flag")).Value == "value-2");
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
            EnvironmentId = "production",
            ApiKey = "api-key",
            CacheTtl = TimeSpan.FromMinutes(1),
            CacheMemoryLimitMegabytes = 1
        });

        Assert.StartsWith("one-", (await client.GetConfigValueAsync("one")).Value);
        Assert.StartsWith("two-", (await client.GetConfigValueAsync("two")).Value);
        Assert.StartsWith("one-", (await client.GetConfigValueAsync("one")).Value);

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
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        var value = await client.GetJsonValueAsync(
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
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        var value = await client.GetConfigValueAsync("flag");

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
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        var value = await client.GetConfigValueAsync("empty");

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
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        var ex = await Assert.ThrowsAsync<NonaClientException>(() =>
            client.GetConfigValueAsync("missing"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Config entry not found", ex.Message);
    }

    [Fact]
    public async Task FailedRequest_ReadsProblemDetailsMessage()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"detail":"Config entry not found","instance":"/api/production/missing"}""",
            HttpStatusCode.NotFound));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nona.test/")
        };

        using var client = new NonaClient(httpClient, new NonaClientOptions
        {
            EnvironmentId = "production",
            ApiKey = "api-key"
        });

        var ex = await Assert.ThrowsAsync<NonaClientException>(() =>
            client.GetConfigValueAsync("missing"));

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

    private static HttpResponseMessage BulkValuesResponse(string json, string etag)
    {
        var response = JsonResponse(json);
        response.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(etag);
        return response;
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

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(await condition(), "Timed out waiting for condition.");
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
