using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Nona.Client;

public sealed partial class NonaClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly NonaClientOptions _options;
    private readonly string? _apiKey;
    private readonly string? _releaseVersion;
    private readonly string _environmentId;
    private readonly string _environmentSegment;
    private readonly object _cacheLock = new object();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<NonaConfigValue>> _inFlightFetches = new(StringComparer.Ordinal);
    private readonly TimeSpan _cacheTtl;
    private readonly long _cacheMemoryLimitBytes;
    private readonly bool _allowStaleCache;
    private long _cacheSizeBytes;

    public NonaClient(string baseAddress, string environmentId, string? apiKey)
        : this(new NonaClientOptions
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            EnvironmentId = environmentId,
            ApiKey = apiKey
        })
    {
    }

    public NonaClient(Uri baseAddress, string environmentId, string? apiKey)
        : this(new NonaClientOptions
        {
            BaseAddress = baseAddress,
            EnvironmentId = environmentId,
            ApiKey = apiKey
        })
    {
    }

    public NonaClient(NonaClientOptions options)
        : this(new HttpClient(), options, disposeHttpClient: true)
    {
    }

    public NonaClient(HttpClient httpClient, string environmentId)
        : this(httpClient, new NonaClientOptions { EnvironmentId = environmentId }, disposeHttpClient: false)
    {
    }

    public NonaClient(HttpClient httpClient, NonaClientOptions options, bool disposeHttpClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _environmentSegment = Segment(_options.EnvironmentId, nameof(NonaClientOptions.EnvironmentId));
        _environmentId = _options.EnvironmentId!;
        _apiKey = _options.ApiKey;
        _releaseVersion = NormalizeReleaseVersion(_options.ReleaseVersion);
        _disposeHttpClient = disposeHttpClient;
        _cacheTtl = ValidateCacheTtl(options.CacheTtl);
        _cacheMemoryLimitBytes = ConvertMegabytesToBytes(ValidateCacheMemoryLimitMegabytes(options.CacheMemoryLimitMegabytes));
        _allowStaleCache = options.AllowStaleCache;

        if (_options.BaseAddress is not null)
        {
            _httpClient.BaseAddress = EnsureTrailingSlash(_options.BaseAddress);
        }
    }

    public string? ApiKey => _apiKey;

    public string? ReleaseVersion => _releaseVersion;

    public string EnvironmentId => _environmentId;

    public async Task<NonaConfigValue> GetConfigValueAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return await GetConfigValueCoreAsync(key, _releaseVersion, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NonaConfigValue> GetConfigValueForReleaseAsync(
        string key,
        string releaseVersion,
        CancellationToken cancellationToken = default)
    {
        return await GetConfigValueCoreAsync(key, releaseVersion, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NonaConfigValue> GetConfigValueCoreAsync(
        string key,
        string? releaseVersion,
        CancellationToken cancellationToken)
    {
        var normalizedReleaseVersion = NormalizeReleaseVersion(releaseVersion);
        var path = BuildConfigValuePath(key, normalizedReleaseVersion);
        var cacheKey = CreateCacheKey(key, normalizedReleaseVersion);
        var cachedValue = TryGetCachedValue(cacheKey, path);
        if (cachedValue is not null)
        {
            return cachedValue;
        }

        return await GetOrFetchConfigValueAsync(cacheKey, path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NonaConfigValue?> TryGetConfigValueAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return await TryGetConfigValueCoreAsync(key, _releaseVersion, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NonaConfigValue?> TryGetConfigValueForReleaseAsync(
        string key,
        string releaseVersion,
        CancellationToken cancellationToken = default)
    {
        return await TryGetConfigValueCoreAsync(key, releaseVersion, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NonaConfigValue?> TryGetConfigValueCoreAsync(
        string key,
        string? releaseVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GetConfigValueCoreAsync(key, releaseVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (NonaClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<string> GetStringValueAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return await GetStringValueCoreAsync(key, _releaseVersion, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetStringValueForReleaseAsync(
        string key,
        string releaseVersion,
        CancellationToken cancellationToken = default)
    {
        return await GetStringValueCoreAsync(key, releaseVersion, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetStringValueCoreAsync(
        string key,
        string? releaseVersion,
        CancellationToken cancellationToken)
    {
        var configValue = await GetConfigValueCoreAsync(key, releaseVersion, cancellationToken).ConfigureAwait(false);
        return configValue.Value;
    }

    public async Task<T?> GetJsonValueAsync<T>(
        string key,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonValueCoreAsync(key, jsonTypeInfo, _releaseVersion, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> GetJsonValueForReleaseAsync<T>(
        string key,
        JsonTypeInfo<T> jsonTypeInfo,
        string releaseVersion,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonValueCoreAsync(key, jsonTypeInfo, releaseVersion, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T?> GetJsonValueCoreAsync<T>(
        string key,
        JsonTypeInfo<T> jsonTypeInfo,
        string? releaseVersion,
        CancellationToken cancellationToken)
    {
        if (jsonTypeInfo is null)
        {
            throw new ArgumentNullException(nameof(jsonTypeInfo));
        }

        var configValue = await GetConfigValueCoreAsync(key, releaseVersion, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(configValue.Value, jsonTypeInfo);
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
