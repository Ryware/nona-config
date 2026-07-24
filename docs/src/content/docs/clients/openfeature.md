---
title: OpenFeature
description: Use Nona through OpenFeature so your application reads flags and config through a standard, vendor-neutral interface with no lock-in.
---

Nona ships OpenFeature integration for JavaScript and .NET. Use OpenFeature when you want a standard flag/config interface, less vendor-specific application code, and cleaner portability at the application layer. It is especially useful when your team thinks in feature flags first but still wants access to the same underlying Nona values and scopes.

## What to set up first

Before wiring OpenFeature into the app:

1. open `Projects`
2. open the project
3. select the target environment
4. create a boolean parameter such as `Features:Checkout`
5. create an API key with the right scope

That gives the provider one real flag to resolve before you expand into broader usage.

## Why use OpenFeature with Nona

Nona already works through plain HTTP and official clients. OpenFeature adds a different benefit: application code depends less on a Nona-specific API, flag reads can look the same across projects, and teams that already use OpenFeature can adopt Nona without inventing a custom abstraction.

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

## Why this pairing works

OpenFeature gives you the application-side abstraction, while Nona still provides the underlying projects, environments, scopes, API keys, history, and rollback. That lets you keep a vendor-neutral read API without giving up a practical self-hosted operations model.

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

Use OpenFeature with Nona when your team already uses OpenFeature, you want feature-flag reads to stay portable, multiple applications should share one flag-reading model, or you want to evaluate flags through a standard API instead of a product-specific client.

## When the direct Nona client is simpler

Use the direct Nona client when you want the smallest dependency surface, the app already uses Nona-specific reads directly, or you need typed Nona value access without the OpenFeature abstraction.

## Good first rollout

Resolve one boolean flag through OpenFeature, confirm the value changes when you edit it in Nona, and only then add more flags. That keeps the rollout grounded in one real runtime path.

## Related docs

- [Feature flags](/docs/feature-flags)
- [What are feature flags?](/docs/feature-flags/what-are-feature-flags)
- [JavaScript client](/docs/clients/javascript)
- [.NET client](/docs/clients/dotnet)
- [Client vs server scope](/docs/concepts/client-vs-server-scope)

JavaScript OpenFeature usage is also included in [JavaScript client](/docs/clients/javascript), and .NET usage is included in [.NET client](/docs/clients/dotnet).

## OpenFeature FAQ

### When is OpenFeature a better fit than the direct client?

OpenFeature is a better fit when your team already uses OpenFeature or wants a vendor-neutral feature-flag interface instead of direct Nona-specific reads.

### Does OpenFeature replace Nona's operational model?

No.

OpenFeature only changes the application-side interface. Nona still provides the projects, environments, scopes, API keys, and history underneath.

### Should I start with OpenFeature immediately?

Usually only if your team already thinks in OpenFeature terms.

Otherwise, many teams start with the direct client or raw HTTP first, then add OpenFeature once the basic read path is proven.

### What is the best first OpenFeature test?

Resolve one boolean flag such as `Features:Checkout` and verify that the application sees the value change after you edit it in Nona.
