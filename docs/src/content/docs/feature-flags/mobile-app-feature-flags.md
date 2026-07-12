---
title: Feature flags for mobile apps
description: Use Nona feature flags in mobile apps to ship safer releases, add kill switches, and control behavior without an app store rollout.
---

Mobile apps benefit from feature flags because app-store releases are slow compared with web deploys.

With Nona, a mobile app can read feature flags at runtime and react without waiting for a new store build.

That makes feature flags especially useful in mobile teams where code can ship before a feature is exposed broadly.

## Common mobile flag use cases

- hide incomplete screens
- disable a broken checkout flow
- turn off a risky integration
- gate a feature that shipped in code but is not ready for everyone

## Why mobile teams need flags

Mobile release cycles are slower and less forgiving than web deploys.

Feature flags help mobile teams:

- ship code before a feature is fully enabled
- keep a fast emergency off-switch
- test staging and production behavior separately
- avoid turning every small runtime adjustment into a store release

## Why Nona fits mobile teams

- self-hosted
- plain HTTP if you do not want a platform-specific SDK dependency
- official JavaScript client for React Native and Node-based tooling
- scopes that help separate client-readable values from backend-only values

## Scope guidance for mobile apps

Use `client` scope for values the app itself should read directly.

Keep truly sensitive decisions on the server where possible. A mobile app can still benefit from the result of a server-side decision without being given direct access to everything.

## Good mobile flag patterns

- keep public flags in `client` scope
- keep truly sensitive decisions server-side when possible
- use kill switches for high-risk features
- pair flags with environment-specific values for staging and production

## Good first mobile flags

- `Features:Checkout`
- `Features:PromoBanner`
- `Features:UseNewOnboarding`
- `Features:DisablePayments`

## Related docs

- [Kill switches](/docs/feature-flags/kill-switches/)
- [JavaScript client](/docs/clients/javascript/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
