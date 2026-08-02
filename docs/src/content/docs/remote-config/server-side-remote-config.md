---
title: Server-side remote config
description: Use Nona for server-side remote config so backend services can read runtime values, feature flags, and operational settings without redeploying.
---

Server-side remote config means backend services read runtime values from a configuration service instead of hardcoding everything into deploy-time environment variables.

Nona is a strong fit for server-side remote config because it is:

- self-hosted
- plain HTTP accessible
- usable from backend services without a mobile SDK
- able to separate server-only values through scope

## Good server-side remote config use cases

Examples:

- feature flags for backend routes
- operational thresholds
- rollout gates for new implementations
- service behavior toggles
- JSON settings for modules or workers

## Why backend teams use it

Server-side remote config helps when:

- a value needs to change without a redeploy
- production and staging should behave differently
- one service needs both feature flags and broader runtime settings
- operators need rollback and auditability for config changes

## Why Nona fits the backend model

Nona keeps the model simple:

- one project per app or service boundary
- one or more environments
- config entries with content types
- `server` scope for backend-only values
- API keys that can be scoped narrowly

That gives backend teams a straightforward operating model without forcing them into a larger hosted platform.

## Recommended patterns

- use `server` scope for backend-only values
- use `boolean` entries for flags and kill switches
- use `number` and `json` for operational settings
- keep environments aligned with real deployment stages
- scope keys to the environment they actually need

## How to create the values

In admin:

1. open `Projects`
2. open the backend service project
3. select the target environment
4. click `Add Parameter`
5. create values such as `Features:UseLegacySearch`, `Limits:MaxItems`, or `App:Settings`
6. choose `server` scope

With the CLI:

```bash
nona entries set \
  --project payments-api \
  --environment production \
  --key Limits:MaxItems \
  --value 50 \
  --scope server \
  --content-type number
```

## How a backend reads the values

In .NET:

```csharp
using Nona.Client;
using System.Globalization;

using var client = new NonaClient(
    "https://nona.example.com",
    "production",
    apiKey: Environment.GetEnvironmentVariable("NONA_API_KEY"));

var maxItemsValue = await client.GetConfigValueAsync("Limits:MaxItems");
var maxItems = int.Parse(maxItemsValue.Value, CultureInfo.InvariantCulture);
```

For a full JSON example, see [.NET client](/docs/clients/dotnet).

If the service is not in .NET, the same values can be fetched with [HTTP](/docs/clients/http).

## Operating model

A practical backend remote-config flow looks like this:

1. keep sensitive runtime values on `server` scope
2. read only the values the service actually needs
3. verify one production-safe read path before widening usage
4. use history and rollback when an operational change goes wrong

## Related docs

- [Feature flags for backend services](/docs/feature-flags/backend-feature-flags)
- [Client vs server scope](/docs/concepts/client-vs-server-scope)
- [HTTP](/docs/clients/http)
- [.NET client](/docs/clients/dotnet)

## FAQ

### What is server-side remote config?

It means backend services read runtime values from a configuration service instead of hardcoding everything into deploy-time settings.

### Should backend remote config use `server` scope?

Usually yes.

Backend-only values should stay on `server` scope whenever possible.

### What is a good first server-side remote-config value?

A threshold such as `Limits:MaxItems` or a boolean flag such as `Features:UseLegacySearch` is usually a strong first example.

### Why is Nona a good fit for server-side remote config?

Because it is self-hosted, plain HTTP accessible, and designed to separate server-only values clearly.
