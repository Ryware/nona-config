---
title: .NET client
description: Read Nona config values from .NET.
---

Package: `Nona.Client`

Targets:

- `netstandard2.0`
- `net8.0`

## Install

```bash
dotnet add package Nona.Client
```

## Read a string

```csharp
using Nona.Client;

var client = new NonaClient(
    "https://nona.example.com",
    apiKey: Environment.GetEnvironmentVariable("NONA_API_KEY"));

var checkout = await client.GetStringValueAsync("production", "Features:Checkout");
var checkoutEnabled = checkout == "true";
```

## Read value metadata

```csharp
var value = await client.GetConfigValueAsync("production", "Features:Checkout");

Console.WriteLine(value.Value);
Console.WriteLine(value.ContentType);
```

`ContentType` is one of `text`, `number`, `boolean`, or `json`.

## Return `null` for missing keys

```csharp
var value = await client.TryGetConfigValueAsync("production", "Missing:Key");

if (value is null)
{
    Console.WriteLine("Key was not found");
}
```

## Read JSON

`GetJsonValueAsync<T>` uses source-generated `JsonTypeInfo<T>`.

```csharp
using System.Text.Json.Serialization;
using Nona.Client;

var settings = await client.GetJsonValueAsync(
    "production",
    "App:Settings",
    AppJsonContext.Default.AppSettings);

public sealed record AppSettings(string Region, int MaxItems);

[JsonSerializable(typeof(AppSettings))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
```

## Optional cache

```csharp
var client = new NonaClient(new NonaClientOptions
{
    BaseAddress = new Uri("https://nona.example.com"),
    ApiKey = Environment.GetEnvironmentVariable("NONA_API_KEY"),
    CacheTtl = TimeSpan.FromSeconds(30),
    CacheMemoryLimitMegabytes = 5,
    AllowStaleCache = true
});
```

## OpenFeature provider

```csharp
using Nona.Client;
using OpenFeature;

var nona = new NonaClient(
    "https://nona.example.com",
    apiKey: Environment.GetEnvironmentVariable("NONA_API_KEY"));

const string domain = "nona-production";

await Api.Instance.SetProviderAsync(
    domain,
    new NonaOpenFeatureProvider(nona, "production"));

var featureClient = Api.Instance.GetClient(domain);
var enabled = await featureClient.GetBooleanValueAsync("Features:Checkout", false);
```
