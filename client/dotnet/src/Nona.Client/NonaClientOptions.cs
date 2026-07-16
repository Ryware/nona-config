using System;

namespace Nona.Client;

public sealed class NonaClientOptions
{
    public Uri? BaseAddress { get; set; }

    public string? EnvironmentId { get; set; }

    public string? ApiKey { get; set; }

    public string? ReleaseVersion { get; set; }

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(30);

    public long CacheMemoryLimitMegabytes { get; set; } = 5;

    public bool AllowStaleCache { get; set; }
}
