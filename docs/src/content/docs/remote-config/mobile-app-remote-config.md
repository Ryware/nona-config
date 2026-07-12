---
title: Remote config for mobile apps
description: Use Nona for mobile remote config so apps can read runtime settings, copy, thresholds, and flags without waiting for a store release.
---

Mobile remote config is about changing app behavior and settings after the app has already shipped.

That can include:

- text shown in the app
- numeric limits
- minimum supported versions
- grouped JSON settings
- feature flags

## Why mobile teams need remote config

Mobile apps move more slowly than web applications because shipping changes usually means going through an app-store release cycle.

Remote config helps mobile teams:

- change values without a new build
- separate staging and production behavior
- roll out new settings gradually
- pair feature flags with broader runtime settings

## Where Nona fits

Nona works well for mobile remote config because it is:

- self-hosted
- open source
- accessible over plain HTTP
- usable with the JavaScript client for React Native and related environments

## Common mobile remote config examples

- `App:MinimumSupportedVersion`
- `App:BannerText`
- `App:Settings`
- `Features:Checkout`

These show how mobile remote config and mobile feature flags often live together in one system.

## Scope guidance

Use `client` scope for values the mobile app should read directly.

Keep truly sensitive logic or backend-only values in `server` scope and let the server evaluate them instead.

## Related docs

- [Feature flags for mobile apps](/docs/feature-flags/mobile-app-feature-flags/)
- [JavaScript client](/docs/clients/javascript/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
