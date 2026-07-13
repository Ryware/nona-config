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

    private static async Task<NonaConfigValue> WaitForFetchAsync(
        Task<NonaConfigValue> fetchTask,
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
            if (!_cache.TryGetValue(cacheKey, out var entry))
            {
                return null;
            }

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

        var oldestKeys = new List<string>(_cache.Count);
        foreach (var item in _cache)
        {
            oldestKeys.Add(item.Key);
        }

        oldestKeys.Sort((left, right) => _cache[left].LastAccessed.CompareTo(_cache[right].LastAccessed));

        foreach (var key in oldestKeys)
        {
            if (_cacheSizeBytes <= _cacheMemoryLimitBytes)
            {
                return;
            }

            RemoveCacheEntry(key);
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

    private static NonaConfigValue Clone(NonaConfigValue value)
    {
        return new NonaConfigValue
        {
            Value = value.Value,
            ContentType = value.ContentType
        };
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
