---
title: Remote config vs environment variables
description: Compare remote config and environment variables, and understand when Nona is a better fit than shipping more values through env vars.
---

Environment variables are good for deployment-time configuration.

Remote config is better when the value should change after deployment.

These two approaches are not enemies. Most teams use both. The question is which values belong in which layer.

## Environment variables fit when

- the value is server-only
- the value changes rarely
- updating it can safely trigger a redeploy or restart
- the value belongs to infrastructure or deployment wiring

## Remote config fits when

- product or operations teams need faster changes
- mobile or client apps need updated values without a new release
- you want one system for feature flags and dynamic settings
- the same app has multiple environments with different runtime values

## A practical way to split them

Use environment variables for things like:

- connection strings
- secret references
- service wiring
- deployment-specific infrastructure settings

Use remote config for things like:

- feature flags
- copy or text values
- numeric thresholds
- JSON settings
- runtime behavior that may change after deploy

## A good Nona split

Keep these in environment variables:

- `NONA_API_KEY` in the consuming app
- database or infrastructure connection strings
- deployment-specific hostnames
- secret material

Keep these in Nona:

- `Features:Checkout`
- `App:BannerText`
- `Limits:MaxItems`
- `App:Settings`

## Why teams outgrow env vars for runtime behavior

Environment variables become awkward when:

- a mobile app needs updated values
- multiple apps should read the same runtime setting
- operations wants a kill switch
- the same key should vary by environment without a redeploy
- rollback history matters

At that point, remote config is usually the cleaner model.

## How this works in practice

A common production pattern is:

1. the app gets its Nona API key from environment variables or a secret manager
2. the app reads runtime values from Nona
3. operators change runtime values in Nona without redeploying the app

That means environment variables and remote config are complementary, not competing systems.

## Nona-specific advantage

Nona gives you a runtime configuration system you host yourself, with:

- projects and environments
- scoped API keys
- client/server scope on entries
- history and rollback

That means Nona can sit beside your deployment-time configuration instead of trying to replace it entirely.

## First implementation step

Keep the application wiring in env vars, then move one runtime value into Nona:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key App:BannerText \
  --value "Free shipping this week" \
  --scope client \
  --content-type text
```

Then read that value from the app through [HTTP](/docs/clients/http/) or an official client.

For first implementation steps, go to [Get started](/docs/get-started/).
