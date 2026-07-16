---
title: .NET client
description: Read Nona config values from .NET.
---

Package: `Nona.Client`

OpenFeature provider package: `Nona.OpenFeature.Provider`

Targets:

- `netstandard2.0`
- `net8.0`

The .NET client is a good fit for:

- ASP.NET applications
- worker services
- console tools
- backend services that want a direct typed integration

## Install

```bash
dotnet add package Nona.Client
```

## Prepare the value in admin

Before wiring the backend:

1. open `Projects`
2. open the backend service project
3. select the target environment such as `production`
4. create the parameter or flag you want to read
5. publish a release and set it active
6. create an API key in the `API Keys` section
7. use `server` scope for backend-only reads whenever possible

For a first backend test, create `Features:UseLegacySearch` as a boolean entry or `App:Settings` as a JSON entry.

## Prepare the value with the CLI

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:UseLegacySearch \
  --value false \
  --scope server \
  --content-type boolean

nona keys create \
  --project storefront \
  --name "API service" \
  --scope server \
  --environment production
```

Then publish and activate a release for the environment in admin.

## Read a string

```csharp
using Nona.Client;

var client = new NonaClient(
    "https://nona.example.com",
    "production",
    apiKey: Environment.GetEnvironmentVariable("NONA_API_KEY"));

var checkout = await client.GetStringValueAsync("Features:Checkout");
var checkoutEnabled = checkout == "true";
```

For feature flags, you will often prefer `boolean` entries and either metadata inspection or OpenFeature depending on how your application is structured.

## Read a boolean flag cleanly

```csharp
var checkout = await client.GetConfigValueAsync("Features:UseLegacySearch");
var useLegacySearch =
    checkout.ContentType == "boolean" &&
    string.Equals(checkout.Value, "true", StringComparison.OrdinalIgnoreCase);
```

That keeps the application aligned with the logical type stored in Nona.

## Read value metadata

```csharp
var value = await client.GetConfigValueAsync("Features:Checkout");

Console.WriteLine(value.Value);
Console.WriteLine(value.ContentType);
```

`ContentType` is one of `text`, `number`, `boolean`, or `json`.

This is useful when one service reads mixed config and needs to branch on the value type.

## Return `null` for missing keys

```csharp
var value = await client.TryGetConfigValueAsync("Missing:Key");

if (value is null)
{
    Console.WriteLine("Key was not found");
}
```

This is helpful for optional settings or for gradual rollout of new keys across environments.

## Pin a release version

By default, reads use the active release selected for the environment.

Set `ReleaseVersion` to pin the client to an exact release or release line:

```csharp
var client = new NonaClient(new NonaClientOptions
{
    BaseAddress = new Uri("https://nona.example.com"),
    EnvironmentId = "production",
    ApiKey = Environment.GetEnvironmentVariable("NONA_API_KEY"),
    ReleaseVersion = "1.1.x"
});
```

Use an exact version such as `1.1.0` for a fixed snapshot. Use a line such as `1.1.x` to read the highest patch in that line.

You can override the configured version for one request:

```csharp
var value = await client.GetConfigValueAsync("Features:Checkout", "1.1.0");
```

## Read JSON

`GetJsonValueAsync<T>` uses source-generated `JsonTypeInfo<T>`.

```csharp
using System.Text.Json.Serialization;
using Nona.Client;

var settings = await client.GetJsonValueAsync(
    "App:Settings",
    AppJsonContext.Default.AppSettings);

public sealed record AppSettings(string Region, int MaxItems);

[JsonSerializable(typeof(AppSettings))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
```

JSON works well when a backend service consumes a cluster of related settings together.

Example Nona setup:

- key: `App:Settings`
- content type: `json`
- scope: `server`

## Default cache

The .NET client caches values in memory by default for 30 seconds. `CacheTtl` changes the cache lifetime and must be greater than zero; `CacheMemoryLimitMegabytes` defaults to `5` and must also be greater than zero. The current client does not expose a cache-disable option.

Set `AllowStaleCache` to return an expired cached value while the client refreshes it in the background.

```csharp
var client = new NonaClient(new NonaClientOptions
{
    BaseAddress = new Uri("https://nona.example.com"),
    EnvironmentId = "production",
    ApiKey = Environment.GetEnvironmentVariable("NONA_API_KEY"),
    CacheTtl = TimeSpan.FromSeconds(30),
    CacheMemoryLimitMegabytes = 5,
    AllowStaleCache = true
});
```

Use `AllowStaleCache` carefully. It can improve resilience and smooth over transient failures, but it also means the application may temporarily serve an older value while refresh happens in the background.

## Basic troubleshooting

If a .NET read fails:

1. confirm the `EnvironmentId` matches the Nona environment name
2. confirm the environment has an active release, or configure `ReleaseVersion`
3. confirm the API key belongs to the same project as the entry
4. confirm the key scope can read the entry scope
5. try the same entry once with [HTTP](/docs/clients/http/) to separate transport issues from application code

## Good first backend flow

A practical first integration looks like this:

1. read one operational flag at startup or request time
2. change it once in admin
3. confirm the service sees the change
4. tune `CacheTtl` only after the read path is working
5. move to OpenFeature when the service becomes flag-oriented

## When to use the .NET client

Use the .NET client when you want:

- a direct Nona integration in C#
- built-in cache behavior
- typed JSON reads
- a simpler path than building your own HTTP wrapper

Use [HTTP](/docs/clients/http/) instead when you only need a minimal raw request path.

## OpenFeature provider

Install the optional provider package:

```bash
dotnet add package Nona.OpenFeature.Provider
```

```csharp
using Nona.Client;
using Nona.OpenFeature.Provider;
using OpenFeature;

var nona = new NonaClient(
    "https://nona.example.com",
    "production",
    apiKey: Environment.GetEnvironmentVariable("NONA_API_KEY"));

const string domain = "nona-production";

await Api.Instance.SetProviderAsync(
    domain,
    new NonaOpenFeatureProvider(nona));

var featureClient = Api.Instance.GetClient(domain);
var enabled = await featureClient.GetBooleanValueAsync("Features:Checkout", false);
```

If your team wants a more flag-oriented, vendor-neutral integration surface, continue with [OpenFeature](/docs/clients/openfeature/).
