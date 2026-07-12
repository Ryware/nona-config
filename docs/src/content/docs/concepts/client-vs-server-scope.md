---
title: Client vs server scope
description: Understand Nona scopes so you can separate frontend-readable config from backend-only values.
---

Scope controls which kinds of consumers should be able to read an entry.

Nona supports:

- `client`
- `server`
- `all`

This is one of the most important Nona concepts because scope affects both:

- which applications should be allowed to read a value
- which API keys can successfully read that value

## Use `client` when

- the value is safe for frontend or mobile apps

Examples:

- feature flags that a mobile app checks directly
- text shown in the UI
- non-sensitive numeric thresholds used by the app client

## Use `server` when

- the value should stay backend-only

Examples:

- server-only rollout gates
- operational thresholds for backend jobs
- values that should not be exposed to frontend code even if the app depends on the result

## Use `all` when

- both kinds of consumers need the same value

Examples:

- a feature flag evaluated in both the frontend and backend
- a shared app behavior toggle used in multiple layers

## Why scope matters

Without scope, it is easy for teams to overexpose config unintentionally.

Nona uses scope to make the intended read surface explicit.

That helps with:

- safer frontend/mobile integrations
- cleaner backend-only control values
- narrower API keys
- clearer operational intent when reading config

## API keys and scope

Scope also matters when you create API keys. Match key scope to the values that app should read.

A few practical examples:

- a React Native app usually needs a `client` key
- a backend service usually needs a `server` key
- `all` should be the exception, not the default

If a key is narrower than the entry scope relationship allows, the read will fail.

## Good habits

- default to the narrowest scope that works
- avoid using `all` just because it is convenient
- keep sensitive decisions on the server when possible
- review scope when creating new API keys

## Related docs

- [API keys](/docs/concepts/api-keys/)
- [Feature flags for mobile apps](/docs/feature-flags/mobile-app-feature-flags/)
- [Feature flags for backend services](/docs/feature-flags/backend-feature-flags/)
