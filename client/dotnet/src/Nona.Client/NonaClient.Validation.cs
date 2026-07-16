using System;

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

    private static string CreateCacheKey(string key, string? releaseVersion)
    {
        return releaseVersion is null ? key : $"{key}\n{releaseVersion}";
    }

    private static string? NormalizeReleaseVersion(string? releaseVersion)
    {
        return string.IsNullOrWhiteSpace(releaseVersion) ? null : releaseVersion!.Trim();
    }

    private static long EstimateCacheEntrySize(string cacheKey, NonaConfigValue value)
    {
        return 128L + (cacheKey.Length + value.Value.Length + value.ContentType.Length) * sizeof(char);
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
