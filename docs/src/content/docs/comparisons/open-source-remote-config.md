---
title: Open Source Remote Config
description: Use Nona for open source, self-hosted remote config with Docker deployment, HTTP access, official clients, and feature flags in the same system.
---

If you are looking for open source remote config, you are usually trying to solve more than one problem at once.

Typical goals:

- change application behavior without redeploying
- keep control of your own infrastructure
- avoid platform lock-in
- use one system for both runtime config and feature flags
- support web, mobile, and backend applications

Nona is built for that combination.

## Why teams look for open source remote config

Common reasons:

- they want a self-hosted deployment model
- they want source visibility and control
- they want to avoid tying runtime config to one vendor ecosystem
- they need remote config for backend services, not only mobile SDK flows
- they want feature flags in the same product

## What Nona provides

Nona gives you:

- open source runtime config
- self-hosted deployment
- Docker-first setup
- plain HTTP access
- official JavaScript and .NET clients
- projects and environments
- typed config entries
- scopes for client and server reads
- history and rollback

## How to try it quickly

The fastest real test is:

1. run the Docker image
2. create one text or number setting
3. create one API key
4. read the value over [HTTP](/docs/clients/http/)

For example:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key App:BannerText \
  --value "Free shipping this week" \
  --scope client \
  --content-type text
```

## Remote config and feature flags together

Nona is not only a remote config system.

It also supports feature flags through the same model:

- `boolean` entries for flags
- `text`, `number`, and `json` for broader runtime config
- the same project/environment/scope/key model for both

That is useful when a team wants one operational surface instead of separate tools for flags and configuration.

## Good fit checklist

Nona is a strong fit if you want:

- open source remote config
- self-hosted deployment
- runtime values for web, mobile, and backend apps
- plain HTTP access
- feature flags in the same product
- a smaller model than a larger hosted platform

## Where Nona fits best

Nona is strongest when you want:

- open source remote config
- self-hosted feature flags
- plain HTTP access from any language
- server-side remote config as well as frontend/mobile reads
- a migration path away from Firebase Remote Config

## Practical examples

Good first Nona remote-config values:

- `App:BannerText`
- `App:MinimumSupportedVersion`
- `Limits:MaxItems`
- `App:Settings`

## What to read next

- [Remote config](/docs/remote-config/)
- [Server-side remote config](/docs/remote-config/server-side-remote-config/)
- [Open source feature flags](/docs/comparisons/open-source-feature-flags/)
- [Get started](/docs/get-started/)
- [Deployment](/docs/deployment/)
