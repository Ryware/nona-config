---
title: OpenFeature
description: Use Nona through OpenFeature so your application reads flags and config through a vendor-neutral interface.
---

Nona ships OpenFeature integration for JavaScript and .NET.

Use OpenFeature when you want:

- a standard flag/config interface
- less vendor-specific application code
- cleaner portability at the application layer

OpenFeature is especially useful when your team thinks in feature flags first but still wants access to the same underlying Nona values and scopes.

## Why use OpenFeature with Nona

Nona already works through plain HTTP and official clients. OpenFeature adds a different benefit:

- your application code depends less on a Nona-specific API
- flag reads can look the same across projects
- teams that already use OpenFeature can adopt Nona without inventing a custom abstraction

This is a good fit when you want self-hosted feature flags but still prefer a vendor-neutral interface at the application layer.

## How Nona maps into OpenFeature

Nona stores values with logical content types:

- `boolean`
- `number`
- `text`
- `json`

That maps cleanly to common OpenFeature value patterns:

- boolean flags
- numeric values
- string values
- object values

In practice, most teams start with boolean flags such as `Features:Checkout`, then add more typed values as needed.

## JavaScript

Install the packages:

```bash
npm install nona-client nona-openfeature-provider @openfeature/server-sdk
```

Basic setup:

```js
import { OpenFeature } from "@openfeature/server-sdk";
import { createNonaOpenFeatureProvider } from "nona-openfeature-provider";

const domain = "nona-production";

await OpenFeature.setProviderAndWait(
  domain,
  createNonaOpenFeatureProvider({
    baseUrl: "https://nona.example.com",
    apiKey: process.env.NONA_API_KEY,
    environmentId: "production"
  })
);

const client = OpenFeature.getClient(domain);
const enabled = await client.getBooleanValue("Features:Checkout", false);
```

The provider only needs:

- Nona base URL
- API key
- environment id

That works because the API key is already bound to one project.

## .NET

Install the packages:

```bash
dotnet add package Nona.Client
dotnet add package Nona.OpenFeature.Provider
```

Basic setup:

```csharp
using Nona.Client;
using Nona.OpenFeature.Provider;
using OpenFeature;

using var nona = new NonaClient(new NonaClientOptions
{
    BaseAddress = new Uri("https://nona.example.com"),
    EnvironmentId = "production",
    ApiKey = Environment.GetEnvironmentVariable("NONA_API_KEY")
});

const string domain = "nona-production";

await Api.Instance.SetProviderAsync(
    domain,
    new NonaOpenFeatureProvider(nona));

var featureClient = Api.Instance.GetClient(domain);
var enabled = await featureClient.GetBooleanValueAsync("Features:Checkout", false);
```

In .NET, the provider is built on top of a configured `NonaClient`, so the OpenFeature provider only needs the client instance.

## When OpenFeature is a good choice

Use OpenFeature with Nona when:

- your team already uses OpenFeature
- you want feature-flag reads to stay portable
- multiple applications should share one flag-reading model
- you want to evaluate flags through a standard API instead of a product-specific client

## When the direct Nona client is simpler

Use the direct Nona client when:

- you want the smallest dependency surface
- the app already uses Nona-specific reads directly
- you need typed Nona value access without the OpenFeature abstraction

## Related docs

- [Feature flags](/docs/feature-flags/)
- [What are feature flags?](/docs/feature-flags/what-are-feature-flags/)
- [JavaScript client](/docs/clients/javascript/)
- [.NET client](/docs/clients/dotnet/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)

## Existing client docs

- JavaScript OpenFeature usage is included in [JavaScript client](/docs/clients/javascript/)
- .NET OpenFeature usage is included in [.NET client](/docs/clients/dotnet/)
