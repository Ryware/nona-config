using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nona.Client;

public sealed partial class NonaClient
{
    private async Task<IReadOnlyDictionary<string, NonaConfigValue>> GetAllValuesCoreAsync(
        string? releaseVersion,
        string? prefix,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedReleaseVersion = NormalizeReleaseVersion(releaseVersion);
        var path = BuildAllConfigValuesPath(normalizedReleaseVersion, prefix);
        var cacheKey = CreateBulkCacheKey(normalizedReleaseVersion, prefix);

        Task<IReadOnlyDictionary<string, NonaConfigValue>>? fetchTask;
        lock (_cacheLock)
        {
            if (!_inFlightBulkFetches.TryGetValue(cacheKey, out fetchTask))
            {
                _bulkCache.TryGetValue(cacheKey, out var previous);
                previous?.Touch();
                fetchTask = FetchAndCacheAllValuesAsync(
                    cacheKey,
                    path,
                    normalizedReleaseVersion,
                    previous);
                _inFlightBulkFetches[cacheKey] = fetchTask;
                TrackInFlightBulkFetch(cacheKey, fetchTask);
            }
        }

        var values = await WaitForFetchAsync(fetchTask, cancellationToken).ConfigureAwait(false);
        return Clone(values);
    }

    private async Task<IReadOnlyDictionary<string, NonaConfigValue>> FetchAndCacheAllValuesAsync(
        string cacheKey,
        string path,
        string? releaseVersion,
        BulkCacheEntry? previous)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        if (!string.IsNullOrWhiteSpace(previous?.Etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", previous!.Etag);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            CancellationToken.None).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified && previous is not null)
        {
            SetBulkCacheEntry(cacheKey, previous.Etag, previous.Values, releaseVersion);
            return Clone(previous.Values);
        }

        var responseBody = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ThrowResponseException(response, request, responseBody);
        }

        var values = DeserializeConfigValues(responseBody);
        var etag = response.Headers.ETag?.ToString();
        SetBulkCacheEntry(cacheKey, etag, values, releaseVersion);
        return Clone(values);
    }

    private void TrackInFlightBulkFetch(
        string cacheKey,
        Task<IReadOnlyDictionary<string, NonaConfigValue>> task)
    {
        _ = task.ContinueWith(
            CompleteInFlightBulkFetch,
            new InFlightBulkFetch(this, cacheKey, task),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void CompleteInFlightBulkFetch(
        Task<IReadOnlyDictionary<string, NonaConfigValue>> completedTask,
        object? state)
    {
        var inFlightFetch = (InFlightBulkFetch)state!;
        var client = inFlightFetch.Client;
        lock (client._cacheLock)
        {
            if (client._inFlightBulkFetches.TryGetValue(inFlightFetch.CacheKey, out var currentTask)
                && ReferenceEquals(currentTask, inFlightFetch.Task))
            {
                client._inFlightBulkFetches.Remove(inFlightFetch.CacheKey);
            }
        }
    }

    private sealed class InFlightBulkFetch
    {
        public InFlightBulkFetch(
            NonaClient client,
            string cacheKey,
            Task<IReadOnlyDictionary<string, NonaConfigValue>> task)
        {
            Client = client;
            CacheKey = cacheKey;
            Task = task;
        }

        public NonaClient Client { get; }

        public string CacheKey { get; }

        public Task<IReadOnlyDictionary<string, NonaConfigValue>> Task { get; }
    }
}
