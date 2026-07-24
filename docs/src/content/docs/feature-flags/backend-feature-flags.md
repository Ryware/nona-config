---
title: Feature flags for backend services
description: Use Nona feature flags in backend services for route gates, operational controls, and server-only rollout decisions.
---

Backend services often need feature flags for operational control more than visual rollout.

Typical backend flag uses:

- enable or disable a route
- switch between old and new implementations
- turn off an expensive integration
- guard risky background jobs

Backend flags are often the highest-leverage flags in a system because they control behavior that the rest of the stack depends on.

## Why server-side flags matter

Server-side flag evaluation lets you:

- keep sensitive behavior off the client
- centralize operational decisions
- hide implementation details from frontend apps

In Nona, that is where `server` scope is especially useful.

## Common backend flag patterns

Examples:

- `Features:UseLegacySearch`
- `Features:DisablePayments`
- `Features:UseAsyncCheckoutWorker`
- `Features:EnableNewRouting`

These names are useful because they describe the behavior the operator is controlling.

## Recommended backend patterns

- use `server` scope for backend-only flags
- keep flag names clear and operationally meaningful
- validate the default behavior when the flag is off
- treat history and rollback as part of your incident path

## How to create one

In admin:

1. open `Projects`
2. open the backend service project
3. select the target environment
4. click `Add Parameter`
5. create a boolean entry such as `Features:DisablePayments`
6. choose `server` scope
7. click `Create`

With the CLI:

```bash
nona entries set \
  --project payments-api \
  --environment production \
  --key Features:DisablePayments \
  --value false \
  --scope server \
  --content-type boolean
```

## How a backend service reads it

In .NET:

```csharp
using Nona.Client;

using var client = new NonaClient(
    "https://nona.example.com",
    "production",
    apiKey: Environment.GetEnvironmentVariable("NONA_API_KEY"));

var flag = await client.GetConfigValueAsync("Features:DisablePayments");
var paymentsDisabled =
    flag.ContentType == "boolean" &&
    string.Equals(flag.Value, "true", StringComparison.OrdinalIgnoreCase);
```

A service in another language can use [HTTP](/docs/clients/http) against the same key.

## How to operate it

When a backend path needs to be disabled:

1. open the parameter row
2. change the value
3. click `Save`
4. review the `History` tab if you need to roll back

For high-risk flags, keep the flag names explicit enough that an operator can safely understand them during an incident.

## Why backend flags pair well with Nona

Nona gives backend teams a practical combination:

- plain HTTP access
- official .NET client support
- feature flags and broader runtime config in one service
- rollback and audit visibility when something changes in production

That makes it a good fit for services that need both operational toggles and runtime settings.

## Related docs

- [Client vs server scope](/docs/concepts/client-vs-server-scope)
- [Kill switches](/docs/feature-flags/kill-switches)
- [.NET client](/docs/clients/dotnet)
- [HTTP](/docs/clients/http)

## FAQ

### Why are backend feature flags important?

They control behavior that the rest of the stack depends on, such as route gates, integrations, and operational toggles.

### Should backend flags use `server` scope?

Usually yes.

Backend-only flags should stay on `server` scope whenever possible.

### Can backend flags work as kill switches?

Yes.

Backend flags are often some of the highest-value kill switches in a system.

### What is a good first backend flag?

A clear operational flag such as `Features:DisablePayments` or `Features:UseLegacySearch` is usually a strong first candidate.
