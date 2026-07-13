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
5. create an API key in the `API Keys` section
6. use `server` scope for backend-only reads whenever possible

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

JSON works well when a backend service consumes a cluster of related settings together. A typical Nona setup is `App:Settings` with content type `json` and scope `server`.

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

If a .NET read fails, confirm `EnvironmentId` matches the Nona environment name, the API key belongs to the same project as the entry, the key scope can read the entry scope, and the same entry works once over [HTTP](/docs/clients/http/).

## Good first backend flow

A practical first integration is to read one operational flag at startup or request time, change it once in admin, confirm the service sees the change, tune `CacheTtl` only after the read path is working, and move to OpenFeature once the service becomes flag-oriented.

## When to use the .NET client

Use the .NET client when you want a direct Nona integration in C#, built-in cache behavior, typed JSON reads, and a simpler path than building your own HTTP wrapper. Use [HTTP](/docs/clients/http/) instead when you only need a minimal raw request path.

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

## .NET client FAQ

### When should I use the .NET client instead of raw HTTP?

Use the .NET client when you want a direct C# integration, built-in cache behavior, typed JSON reads, and a simpler path than maintaining your own HTTP wrapper.

### Does the .NET client cache values?

Yes.

The .NET client caches values in memory by default, and you can tune the TTL and memory limit through `NonaClientOptions`.

### Should backend services prefer `server` scope?

Usually yes.

Backend-only values should use `server` scope whenever possible so the read surface stays as narrow as possible.

### When should I use the OpenFeature provider?

Use the OpenFeature provider when the service is becoming more flag-oriented and you want a vendor-neutral evaluation interface on top of the Nona client.
