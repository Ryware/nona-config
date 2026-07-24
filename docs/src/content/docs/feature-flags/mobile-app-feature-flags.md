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

## How to create one

In admin:

1. open `Projects`
2. open the mobile app project
3. select the environment such as `staging` or `production`
4. click `Add Parameter`
5. create a boolean entry such as `Features:UseNewOnboarding`
6. choose `client` scope
7. click `Create`

With the CLI:

```bash
nona entries set \
  --project mobile-app \
  --environment production \
  --key Features:UseNewOnboarding \
  --value false \
  --scope client \
  --content-type boolean
```

## How a mobile app reads it

React Native or other JavaScript-based mobile runtimes can read the flag with the JavaScript client:

```js
import { createNonaClient } from "nona-client";

const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  environmentId: "production",
  apiKey: process.env.NONA_API_KEY
});

const flag = await nona.getConfigValue("Features:UseNewOnboarding");
const enabled = flag.contentType === "boolean" && flag.value === "true";
```

## How to operate it

When a mobile feature needs to be disabled quickly:

1. open the parameter row in admin
2. switch the value from `true` to `false`
3. click `Save`
4. verify the app sees the updated value

For risky features, also confirm the `false` path works before release day.

## Related docs

- [Kill switches](/docs/feature-flags/kill-switches)
- [JavaScript client](/docs/clients/javascript)
- [Client vs server scope](/docs/concepts/client-vs-server-scope)

## FAQ

### Why do mobile apps benefit so much from feature flags?

Because mobile release cycles are slower than web deploys, and flags let teams change behavior without waiting for another store release.

### Should mobile flags use `client` scope?

Usually yes for values the app reads directly.

Keep sensitive decisions on the server when possible.

### What is a good first mobile feature flag?

A flag such as `Features:UseNewOnboarding` or `Features:Checkout` is usually a good first test because the behavior is easy to see.

### Can mobile feature flags also work as kill switches?

Yes.

That is one of the strongest uses of flags in mobile applications.
