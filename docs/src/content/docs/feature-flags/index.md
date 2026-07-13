---
title: Feature flags
description: Use Nona for self-hosted, open source feature flags with kill switches, scoped reads, OpenFeature support, and runtime control.
---

Nona is a self-hosted feature flag system as well as a remote config system.

In Nona, a feature flag is usually a config entry with content type `boolean`. That sounds simple, but it gives you the core behavior most teams actually need:

- turn a feature on or off without redeploying
- add a kill switch for risky code paths
- separate frontend-readable and backend-only flags
- keep flag changes in your own infrastructure
- audit and roll back bad changes quickly

## Why teams use Nona for feature flags

Many feature flag tools are hosted services or part of larger closed platforms.

Nona is different:

- open source
- self-hosted
- Docker-first
- accessible over plain HTTP
- usable with official JavaScript and .NET clients
- integrated with OpenFeature

## What a flag looks like in Nona

A typical feature flag looks like this:

- key: `Features:Checkout`
- value: `true`
- content type: `boolean`
- scope: `client`, `server`, or `all`

That same model also supports more than flags. If a value needs to become text, numeric, or JSON later, you are still inside the same system.

## Quick start for flags

In admin:

1. open `Projects`
2. open the project
3. select the environment
4. click `Add Parameter`
5. create a boolean key such as `Features:Checkout`
6. choose `client` or `server`

With the CLI:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value true \
  --scope client \
  --content-type boolean
```

## Use cases

Common feature flag use cases in Nona:

- kill switches
- staged frontend releases
- backend route gates
- hiding incomplete UI
- operational toggles
- environment-specific enablement

## How teams usually progress

Most teams start with:

1. one kill switch
2. one staged release flag
3. one backend operational toggle

That is usually enough to establish a real feature-flag workflow before expanding further.

## Start here

- [What are feature flags?](/docs/feature-flags/what-are-feature-flags/)
- [Feature flags vs remote config](/docs/feature-flags/feature-flags-vs-remote-config/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [Feature flags for mobile apps](/docs/feature-flags/mobile-app-feature-flags/)
- [Feature flags for backend services](/docs/feature-flags/backend-feature-flags/)
- [Open source feature flags](/docs/comparisons/open-source-feature-flags/)
- [Self-hosted feature flags](/docs/comparisons/self-hosted-feature-flags/)
- [OpenFeature with Nona](/docs/clients/openfeature/)

## Related comparisons

- [Open source feature flags](/docs/comparisons/open-source-feature-flags/)
- [Self-hosted feature flags](/docs/comparisons/self-hosted-feature-flags/)
- [Open source remote config](/docs/comparisons/open-source-remote-config/)

## FAQ

### Is Nona only a feature flag tool?

No.

Nona supports feature flags and broader remote config in the same system. Feature flags are one major use case, not the whole product.

### How do feature flags work in Nona?

Most feature flags in Nona are boolean config entries.

That gives teams a simple operational model for toggles, kill switches, and release gates without needing a separate control plane.

### Can Nona handle backend and frontend flags?

Yes.

The scope model allows client-readable, server-only, and shared reads depending on where the flag should be evaluated.

### When is Nona a good fit for feature flags?

Nona is a strong fit when you want self-hosted, open source feature flags with simpler operations and one product for flags and runtime config.
