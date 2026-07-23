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

Reads use the environment's active release by default. To pin a client to an exact release or release line:

```csharp
var client = new NonaClient(new NonaClientOptions
{
    BaseAddress = new Uri("https://nona.example.com"),
    EnvironmentId = "production",
    ApiKey = "your-api-key",
    ReleaseVersion = "1.1.x"
});
```

To select a different release for one request, use the corresponding named release method:

```csharp
var value = await client.GetConfigValueForReleaseAsync("Features:Checkout", "1.1.0");
```

## Available Methods

- `GetConfigValueAsync(string key, CancellationToken cancellationToken = default)`
- `GetConfigValueForReleaseAsync(string key, string releaseVersion, CancellationToken cancellationToken = default)`
- `TryGetConfigValueAsync(string key, CancellationToken cancellationToken = default)`
- `TryGetConfigValueForReleaseAsync(string key, string releaseVersion, CancellationToken cancellationToken = default)`
- `GetStringValueAsync(string key, CancellationToken cancellationToken = default)`
- `GetStringValueForReleaseAsync(string key, string releaseVersion, CancellationToken cancellationToken = default)`
- `GetJsonValueAsync<T>(string key, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)`
- `GetJsonValueForReleaseAsync<T>(string key, JsonTypeInfo<T> jsonTypeInfo, string releaseVersion, CancellationToken cancellationToken = default)`

## Options

Use `NonaClientOptions` to configure:

- `BaseAddress`
- `EnvironmentId`
- `ApiKey`
- `ReleaseVersion`
- `CacheTtl`
- `CacheMemoryLimitMegabytes`
- `AllowStaleCache`
