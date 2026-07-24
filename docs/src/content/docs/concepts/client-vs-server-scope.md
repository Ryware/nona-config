---
title: Client vs server scope
description: Understand Nona client, server, and shared scopes so you can separate frontend-readable config from backend-only secret values.
---

Scope controls which kinds of consumers should be able to read an entry.

Nona supports:

- `client`
- `server`
- `all`

This is one of the most important Nona concepts because scope affects which applications should be allowed to read a value and which API keys can successfully read that value.

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

Without scope, it is easy for teams to overexpose config unintentionally. Nona uses scope to make the intended read surface explicit, which helps with safer frontend/mobile integrations, cleaner backend-only control values, narrower API keys, and clearer operational intent when reading config.

## How to choose scope in admin

When you create a parameter:

1. open `Projects`
2. open the project
3. select the environment
4. click `Add Parameter`
5. choose the content type
6. choose the scope based on who should read it

A practical rule is to choose `client` if the app itself reads the value, `server` if only backend services should read it, and `all` only when both truly need the same entry.

## API keys and scope

Scope also matters when you create API keys. Match key scope to the values that app should read.

A few practical examples: a React Native app usually needs a `client` key, a backend service usually needs a `server` key, and `all` should be the exception, not the default.

If a key is narrower than the entry scope relationship allows, the read will fail.

## CLI examples

Client-readable flag:

```bash
nona entries set \
  --project mobile-app \
  --environment production \
  --key Features:PromoBanner \
  --value true \
  --scope client \
  --content-type boolean
```

Server-only threshold:

```bash
nona entries set \
  --project payments-api \
  --environment production \
  --key Limits:RetryCount \
  --value 5 \
  --scope server \
  --content-type number
```

Then create matching keys:

```bash
nona keys create --project mobile-app --name "Mobile app" --scope client --environment production
nona keys create --project payments-api --name "Payments API" --scope server --environment production
```

## Good habits

- default to the narrowest scope that works
- avoid using `all` just because it is convenient
- keep sensitive decisions on the server when possible
- review scope when creating new API keys

## FAQ

### What scope should I choose first?

Choose the narrowest scope that matches the real read surface.

For many frontend or mobile reads, that is `client`. For backend-only values, that is `server`.

### When should I use `all`?

Only when both frontend and backend genuinely need to read the same value.

It should be the exception, not the default.

### Can a `boolean` flag be `server` scope?

Yes.

Feature flags are not automatically client-side. A boolean flag can be `client`, `server`, or `all` depending on where it is evaluated.

### What is the biggest scope mistake?

Using broader scope than necessary.

That makes values easier to expose accidentally and weakens the access model.

## Related docs

- [API keys](/docs/concepts/api-keys/)
- [Feature flags for mobile apps](/docs/feature-flags/mobile-app-feature-flags/)
- [Feature flags for backend services](/docs/feature-flags/backend-feature-flags/)
