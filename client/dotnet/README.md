# Nona.Client (.NET)

Official .NET/C# client for **Nona** — an open-source, self-hosted remote configuration and feature flag service, and a Firebase Remote Config alternative you run yourself. Read your config values and feature flags at runtime with a single typed call. Targets `netstandard2.0` and `net8.0`.

- Website: https://nonaconfig.com
- Source & docs: https://github.com/Ryware/nona-config/tree/development/client/dotnet

## Project

- Package ID: `Nona.Client`
- Source project: [dotnet/src/Nona.Client/Nona.Client.csproj](src/Nona.Client/Nona.Client.csproj)
- Test project: [dotnet/tests/Nona.Client.Tests/Nona.Client.Tests.csproj](tests/Nona.Client.Tests/Nona.Client.Tests.csproj)

## Target Frameworks

- `netstandard2.0`
- `net8.0`

## Basic Usage

```csharp
using Nona.Client;

var client = new NonaClient("https://nona.example.com", "production", apiKey: "your-api-key");
var value = await client.GetConfigValueAsync("Features:Checkout");
Console.WriteLine(value.Value);
```

API keys are bound to one project, and the client is bound to one environment, so config reads only take a key.

Reads use working parameters by default. To use the active release, or pin the client to an exact release or release line, configure release mode when constructing it:

```csharp
var client = new NonaClient(new NonaClientOptions
{
    BaseAddress = new Uri("https://nona.example.com"),
    EnvironmentId = "production",
    ApiKey = "your-api-key",
    UseReleases = true,
    ReleaseVersion = "1.1.x"
});
```

Omit `ReleaseVersion` while keeping `UseReleases = true` to follow the active release. Source and release selection are fixed for the client lifetime; construct another client to use a different source or selector. When `UseReleases` is `false` (the default), `ReleaseVersion` is retained in the client options but ignored for requests and cache identity.

Fetch all client-visible values, or only keys in a case-insensitive prefix group:

```csharp
IReadOnlyDictionary<string, NonaConfigValue> all = await client.GetAllValuesAsync();
IReadOnlyDictionary<string, NonaConfigValue> groupA = await client.GetAllValuesAsync("GroupA:");
```

Prefixes may contain ASCII letters, digits, colons, dots, underscores, and dashes. Empty and `null` prefixes are unfiltered. Any other character causes `NonaClientException` with `StatusCode == HttpStatusCode.BadRequest`; failed responses are not cached. Bulk snapshots use independent ETags per release and normalized prefix, prime matching single-key reads, and participate in the shared memory limit.

## Available Methods

- `GetAllValuesAsync(string? prefix = null, CancellationToken cancellationToken = default)`
- `GetConfigValueAsync(string key, CancellationToken cancellationToken = default)`
- `TryGetConfigValueAsync(string key, CancellationToken cancellationToken = default)`
- `GetStringValueAsync(string key, CancellationToken cancellationToken = default)`
- `GetJsonValueAsync<T>(string key, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)`

## Options

Use `NonaClientOptions` to configure:

- `BaseAddress`
- `EnvironmentId`
- `ApiKey`
- `UseReleases`
- `ReleaseVersion`
- `CacheTtl`
- `CacheMemoryLimitMegabytes`
- `AllowStaleCache`
