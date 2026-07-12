---
title: Feature flags for mobile apps
description: Use Nona feature flags in mobile apps to ship safer releases, add kill switches, and control behavior without an app store rollout.
---

Mobile apps benefit from feature flags because app-store releases are slow compared with web deploys.

With Nona, a mobile app can read feature flags at runtime and react without waiting for a new store build.

## Common mobile flag use cases

- hide incomplete screens
- disable a broken checkout flow
- turn off a risky integration
- gate a feature that shipped in code but is not ready for everyone

## Why Nona fits mobile teams

- self-hosted
- plain HTTP if you do not want a platform-specific SDK dependency
- official JavaScript client for React Native and Node-based tooling
- scopes that help separate client-readable values from backend-only values

## Good mobile flag patterns

- keep public flags in `client` scope
- keep truly sensitive decisions server-side when possible
- use kill switches for high-risk features
- pair flags with environment-specific values for staging and production

## Related docs

- [Kill switches](/docs/feature-flags/kill-switches/)
- [JavaScript client](/docs/clients/javascript/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
