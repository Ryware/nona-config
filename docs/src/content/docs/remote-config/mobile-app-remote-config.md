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

That makes mobile remote config broader than feature flags alone. A mobile app often needs both:

- boolean release gates
- non-boolean runtime values

## Why mobile teams need remote config

Mobile apps move more slowly than web applications because shipping changes usually means going through an app-store release cycle.

Remote config helps mobile teams:

- change values without a new build
- separate staging and production behavior
- roll out new settings gradually
- pair feature flags with broader runtime settings

## Typical mobile remote config values

Examples:

- minimum supported version
- banner text or in-app copy
- module-specific JSON settings
- numeric limits or thresholds
- feature flags for incomplete or risky flows

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

## Why this matters in Nona

Nona is useful here because it does not force mobile teams into a hosted platform model just to get runtime values.

You can keep:

- self-hosted deployment
- official client access where it helps
- plain HTTP as a fallback
- one system for both mobile flags and mobile remote config

## Related docs

- [Feature flags for mobile apps](/docs/feature-flags/mobile-app-feature-flags/)
- [JavaScript client](/docs/clients/javascript/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
