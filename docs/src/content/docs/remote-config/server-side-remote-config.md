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

## Related docs

- [Feature flags for backend services](/docs/feature-flags/backend-feature-flags/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
- [HTTP](/docs/clients/http/)
- [.NET client](/docs/clients/dotnet/)
