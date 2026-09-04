using System;
using System.Collections.Generic;

namespace Nona.Client;

public sealed partial class NonaClient
{
    private static string Segment(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return Uri.EscapeDataString(value);
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var value = uri.ToString();
        return value.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(value + "/", UriKind.Absolute);
    }

    private string BuildConfigValuePath(string key, string? releaseVersion)
    {
        var path = $"api/{_environmentSegment}/{Segment(key, nameof(key))}";
        return releaseVersion is null
            ? path
            : $"{path}?version={Uri.EscapeDataString(releaseVersion)}";
    }

    private string BuildAllConfigValuesPath(string? releaseVersion, string? prefix)
    {
        var path = $"api/{_environmentSegment}";
        var query = new List<string>(2);
        if (releaseVersion is not null)
        {
            query.Add($"version={Uri.EscapeDataString(releaseVersion)}");
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            query.Add($"prefix={Uri.EscapeDataString(prefix)}");
        }

        return query.Count == 0 ? path : $"{path}?{string.Join("&", query)}";
    }

    private static string CreateCacheKey(string key, string? releaseVersion)
    {
        return releaseVersion is null ? key : $"{key}\n{releaseVersion}";
    }

    private static string CreateBulkCacheKey(string? releaseVersion, string? prefix)
    {
        var normalizedPrefix = NormalizePrefix(prefix);
        return $"{releaseVersion ?? string.Empty}\n{normalizedPrefix ?? string.Empty}";
    }

    private static string? NormalizeReleaseVersion(string? releaseVersion)
    {
        return string.IsNullOrWhiteSpace(releaseVersion) ? null : releaseVersion!.Trim();
    }

    private static string? NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return null;
        }

        var normalized = prefix!.ToCharArray();
        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];
            normalized[index] = character is >= 'a' and <= 'z'
                ? (char)(character - ('a' - 'A'))
                : character;
        }

        return new string(normalized);
    }

    private static long EstimateCacheEntrySize(string cacheKey, NonaConfigValue value)
    {
        return 128L + (cacheKey.Length + value.Value.Length + value.ContentType.Length) * sizeof(char);
    }

    private static long EstimateBulkCacheEntrySize(
        string cacheKey,
        string? etag,
        IReadOnlyDictionary<string, NonaConfigValue> values,
        IReadOnlyDictionary<string, string> requestKeys)
    {
        var sizeBytes = 192L + (cacheKey.Length + (etag?.Length ?? 0)) * sizeof(char);
        foreach (var pair in values)
        {
            sizeBytes += (pair.Key.Length + pair.Value.Value.Length + pair.Value.ContentType.Length) * sizeof(char);
        }

        foreach (var pair in requestKeys)
        {
            sizeBytes += (pair.Key.Length + pair.Value.Length) * sizeof(char);
        }

        return sizeBytes;
    }

    private static TimeSpan ValidateCacheTtl(TimeSpan cacheTtl)
    {
        if (cacheTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cacheTtl), cacheTtl, "Cache TTL must be greater than zero.");
        }

        return cacheTtl;
    }

    private static long ValidateCacheMemoryLimitMegabytes(long cacheMemoryLimitMegabytes)
    {
        if (cacheMemoryLimitMegabytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cacheMemoryLimitMegabytes), cacheMemoryLimitMegabytes, "Cache memory limit must be greater than zero.");
        }

        if (cacheMemoryLimitMegabytes > long.MaxValue / 1024 / 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(cacheMemoryLimitMegabytes), cacheMemoryLimitMegabytes, "Cache memory limit is too large.");
        }

        return cacheMemoryLimitMegabytes;
    }

    private static long ConvertMegabytesToBytes(long megabytes)
    {
        return megabytes * 1024 * 1024;
    }
}
