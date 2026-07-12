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

## How to create the values

In admin:

1. open `Projects`
2. open the mobile app project
3. select the environment
4. click `Add Parameter`
5. create values such as `App:MinimumSupportedVersion`, `App:BannerText`, or `App:Settings`
6. choose `client` scope for values the app reads directly

With the CLI:

```bash
nona entries set \
  --project mobile-app \
  --environment production \
  --key App:BannerText \
  --value "Free shipping this week" \
  --scope client \
  --content-type text
```

## How a mobile app reads the values

```js
import { createNonaClient } from "nona-client";

const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  environmentId: "production",
  apiKey: process.env.NONA_API_KEY
});

const bannerText = await nona.getStringValue("App:BannerText");
const settings = await nona.getJsonValue("App:Settings");
```

This is the basic mobile remote-config path: small text or numeric values directly, and grouped settings through JSON when they naturally belong together.

## Scope guidance

Use `client` scope for values the mobile app should read directly.

Keep truly sensitive logic or backend-only values in `server` scope and let the server evaluate them instead.

## Operational pattern

For most mobile teams:

1. start with one or two directly visible values such as banner text
2. add one kill switch for a risky flow
3. keep production and staging separate
4. use JSON only when the values truly belong together

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
