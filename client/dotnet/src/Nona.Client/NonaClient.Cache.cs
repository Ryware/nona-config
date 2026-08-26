using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nona.Client;

public sealed partial class NonaClient
{
    private async Task<NonaConfigValue> GetOrFetchConfigValueAsync(
        string cacheKey,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task<NonaConfigValue>? fetchTask;
        lock (_cacheLock)
        {
            if (!_inFlightFetches.TryGetValue(cacheKey, out fetchTask))
            {
                fetchTask = FetchAndCacheConfigValueAsync(cacheKey, path);
                _inFlightFetches[cacheKey] = fetchTask;
                TrackInFlightFetch(cacheKey, fetchTask);
            }
        }

        var value = await WaitForFetchAsync(fetchTask, cancellationToken).ConfigureAwait(false);
        return Clone(value);
    }

    private void TrackInFlightFetch(string cacheKey, Task<NonaConfigValue> task)
    {
        _ = task.ContinueWith(
            CompleteInFlightFetch,
            new InFlightFetch(this, cacheKey, task),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task<NonaConfigValue> FetchAndCacheConfigValueAsync(string cacheKey, string path)
    {
        var value = await FetchConfigValueAsync(path, CancellationToken.None).ConfigureAwait(false);
        SetCachedValue(cacheKey, value);
        return value;
    }

    private static void CompleteInFlightFetch(Task<NonaConfigValue> completedTask, object? state)
    {
        var inFlightFetch = (InFlightFetch)state!;
        var client = inFlightFetch.Client;

        lock (client._cacheLock)
        {
            if (client._inFlightFetches.TryGetValue(inFlightFetch.CacheKey, out var currentTask) &&
                ReferenceEquals(currentTask, inFlightFetch.Task))
            {
                client._inFlightFetches.Remove(inFlightFetch.CacheKey);
            }
        }
    }

    private static async Task<T> WaitForFetchAsync<T>(
        Task<T> fetchTask,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || fetchTask.IsCompleted)
        {
            return await fetchTask.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var cancellationTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(
            state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            cancellationTaskSource))
        {
            var completedTask = await Task.WhenAny(fetchTask, cancellationTaskSource.Task).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, fetchTask))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return await fetchTask.ConfigureAwait(false);
    }

    private NonaConfigValue? TryGetCachedValue(string cacheKey, string path)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                var now = DateTimeOffset.UtcNow;
                if (entry.ExpiresAt > now)
                {
                    entry.Touch();
                    return Clone(entry.Value);
                }

                if (_allowStaleCache)
                {
                    entry.Touch();
                    QueueRefresh(cacheKey, path, entry);
                    return Clone(entry.Value);
                }

                RemoveCacheEntry(cacheKey);
            }

            if (_primedValues.TryGetValue(cacheKey, out var primed)
                && _bulkCache.TryGetValue(primed.BulkKey, out var bulk)
                && bulk.Values.TryGetValue(primed.ValueKey, out var primedValue))
            {
                bulk.Touch();
                return Clone(primedValue);
            }

            _primedValues.Remove(cacheKey);
            return null;
        }
    }

    private void QueueRefresh(string cacheKey, string path, CacheEntry entry)
    {
        if (entry.Refreshing)
        {
            return;
        }

        entry.Refreshing = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var value = await FetchConfigValueAsync(path, CancellationToken.None).ConfigureAwait(false);
                SetCachedValue(cacheKey, value);
            }
            catch
            {
                lock (_cacheLock)
                {
                    if (_cache.TryGetValue(cacheKey, out var current))
                    {
                        current.Refreshing = false;
                    }
                }
            }
        });
    }

    private void SetCachedValue(string cacheKey, NonaConfigValue value)
    {
        var cachedValue = Clone(value);
        var sizeBytes = EstimateCacheEntrySize(cacheKey, cachedValue);
        if (sizeBytes > _cacheMemoryLimitBytes)
        {
            lock (_cacheLock)
            {
                RemoveCacheEntry(cacheKey);
            }

            return;
        }

        lock (_cacheLock)
        {
            RemoveCacheEntry(cacheKey);
            _cache[cacheKey] = new CacheEntry(cachedValue, DateTimeOffset.UtcNow.Add(_cacheTtl), sizeBytes);
            _cacheSizeBytes += sizeBytes;
            CompactCache();
        }
    }

    private void CompactCache()
    {
        if (_cacheSizeBytes <= _cacheMemoryLimitBytes)
        {
            return;
        }

        var candidates = new List<CacheCandidate>(_cache.Count + _bulkCache.Count);
        foreach (var item in _cache)
        {
            candidates.Add(new CacheCandidate(CacheKind.Single, item.Key, item.Value.LastAccessed));
        }

        foreach (var item in _bulkCache)
        {
            candidates.Add(new CacheCandidate(CacheKind.Bulk, item.Key, item.Value.LastAccessed));
        }

        candidates.Sort((left, right) => left.LastAccessed.CompareTo(right.LastAccessed));

        foreach (var candidate in candidates)
        {
            if (_cacheSizeBytes <= _cacheMemoryLimitBytes)
            {
                return;
            }

            if (candidate.Kind == CacheKind.Single)
            {
                RemoveCacheEntry(candidate.Key);
            }
            else
            {
                RemoveBulkCacheEntry(candidate.Key);
            }
        }
    }

    private void RemoveCacheEntry(string cacheKey)
    {
        if (!_cache.TryGetValue(cacheKey, out var entry))
        {
            return;
        }

        _cache.Remove(cacheKey);
        _cacheSizeBytes -= entry.SizeBytes;
    }

    private void SetBulkCacheEntry(
        string cacheKey,
        string? etag,
        IReadOnlyDictionary<string, NonaConfigValue> values,
        string? releaseVersion)
    {
        var cachedValues = Clone(values);
        var requestKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in cachedValues)
        {
            requestKeys[CreateCacheKey(pair.Key, releaseVersion)] = pair.Key;
        }

        var sizeBytes = EstimateBulkCacheEntrySize(cacheKey, etag, cachedValues, requestKeys);
        lock (_cacheLock)
        {
            RemoveBulkCacheEntry(cacheKey);
            foreach (var requestKey in requestKeys.Keys)
            {
                RemoveCacheEntry(requestKey);
            }

            if (sizeBytes > _cacheMemoryLimitBytes)
            {
                return;
            }

            var entry = new BulkCacheEntry(etag, cachedValues, requestKeys, sizeBytes);
            _bulkCache[cacheKey] = entry;
            _cacheSizeBytes += sizeBytes;
            foreach (var pair in requestKeys)
            {
                _primedValues[pair.Key] = new PrimedValue(cacheKey, pair.Value);
            }

            CompactCache();
        }
    }

    private void RemoveBulkCacheEntry(string cacheKey)
    {
        if (!_bulkCache.TryGetValue(cacheKey, out var entry))
        {
            return;
        }

        _bulkCache.Remove(cacheKey);
        _cacheSizeBytes -= entry.SizeBytes;
        foreach (var requestKey in entry.RequestKeys.Keys)
        {
            if (_primedValues.TryGetValue(requestKey, out var primed)
                && string.Equals(primed.BulkKey, cacheKey, StringComparison.Ordinal))
            {
                _primedValues.Remove(requestKey);
            }
        }
    }

    private static NonaConfigValue Clone(NonaConfigValue value)
    {
        return new NonaConfigValue
        {
            Value = value.Value,
            ContentType = value.ContentType
        };
    }

    private static Dictionary<string, NonaConfigValue> Clone(
        IReadOnlyDictionary<string, NonaConfigValue> values)
    {
        var clone = new Dictionary<string, NonaConfigValue>(values.Count, StringComparer.Ordinal);
        foreach (var pair in values)
        {
            clone[pair.Key] = Clone(pair.Value);
        }

        return clone;
    }

    private sealed class CacheEntry
    {
        public CacheEntry(NonaConfigValue value, DateTimeOffset expiresAt, long sizeBytes)
        {
            Value = value;
            ExpiresAt = expiresAt;
            SizeBytes = sizeBytes;
            LastAccessed = DateTimeOffset.UtcNow;
        }

        public NonaConfigValue Value { get; }

        public DateTimeOffset ExpiresAt { get; }

        public long SizeBytes { get; }

        public DateTimeOffset LastAccessed { get; private set; }

        public bool Refreshing { get; set; }

        public void Touch()
        {
            LastAccessed = DateTimeOffset.UtcNow;
        }
    }

    private sealed class BulkCacheEntry
    {
        public BulkCacheEntry(
            string? etag,
            Dictionary<string, NonaConfigValue> values,
            Dictionary<string, string> requestKeys,
            long sizeBytes)
        {
            Etag = etag;
            Values = values;
            RequestKeys = requestKeys;
            SizeBytes = sizeBytes;
            LastAccessed = DateTimeOffset.UtcNow;
        }

        public string? Etag { get; }

        public Dictionary<string, NonaConfigValue> Values { get; }

        public Dictionary<string, string> RequestKeys { get; }

        public long SizeBytes { get; }

        public DateTimeOffset LastAccessed { get; private set; }

        public void Touch()
        {
            LastAccessed = DateTimeOffset.UtcNow;
        }
    }

    private sealed class PrimedValue
    {
        public PrimedValue(string bulkKey, string valueKey)
        {
            BulkKey = bulkKey;
            ValueKey = valueKey;
        }

        public string BulkKey { get; }

        public string ValueKey { get; }
    }

    private readonly struct CacheCandidate
    {
        public CacheCandidate(CacheKind kind, string key, DateTimeOffset lastAccessed)
        {
            Kind = kind;
            Key = key;
            LastAccessed = lastAccessed;
        }

        public CacheKind Kind { get; }

        public string Key { get; }

        public DateTimeOffset LastAccessed { get; }
    }

    private enum CacheKind
    {
        Single,
        Bulk
    }

    private sealed class InFlightFetch
    {
        public InFlightFetch(NonaClient client, string cacheKey, Task<NonaConfigValue> task)
        {
            Client = client;
            CacheKey = cacheKey;
            Task = task;
        }

        public NonaClient Client { get; }

        public string CacheKey { get; }

        public Task<NonaConfigValue> Task { get; }
    }
}
