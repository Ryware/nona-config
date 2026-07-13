---
title: Kill switches
description: Use Nona kill switches to disable risky features quickly with boolean flags, scoped reads, and rollback support.
---

A kill switch is a feature flag whose main job is to turn something off fast, which makes it one of the most valuable Nona patterns because it gives you an operational escape hatch without a redeploy.

## Basic pattern

Create a boolean entry such as:

- key: `Features:Checkout`
- value: `true`
- content type: `boolean`
- scope: `client`, `server`, or `all`

When the feature needs to be disabled, set the value to `false`.

## How to create a kill switch

In admin:

1. open `Projects`
2. open the project
3. select the target environment
4. click `Add Parameter`
5. create a boolean key such as `Features:Checkout`
6. choose the correct scope
7. click `Create`

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

That gives you a live switch you can flip later without redeploying the app.

## When to use a kill switch

Use kill switches for:

- unstable new features
- third-party integration failures
- broken frontend flows
- backend routes that need fast shutdown
- expensive or risky behavior you may need to disable under load

## Picking the right scope

Choose scope based on where the feature is evaluated:

- use `client` when the app UI or mobile client checks the flag
- use `server` when only backend services should read it
- use `all` when both sides must respect the same switch

If you can keep the decision server-only, that is usually safer.

## How to operate it during an incident

In admin:

1. open the parameter row
2. stay on the `Settings` tab
3. change the value from `true` to `false`
4. click `Save`
5. verify the application behavior changes

With the CLI:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value false \
  --scope client \
  --content-type boolean
```

## Operational benefits in Nona

Nona makes kill switches more useful because they fit into the rest of the product model: the flag lives in a project and environment, API keys control who can read it, history shows previous values, rollback gives you a fast recovery path, and audit logs help explain who changed it.

## Roll back the switch

If you need to restore a previous known-good state:

```bash
nona entries history \
  --project storefront \
  --environment production \
  --key Features:Checkout

nona entries rollback \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --version 2
```

In admin, open the parameter, switch to `History`, and use `Rollback to v...`.

## Good kill switch habits

- default the application to the safest behavior when the flag is off
- keep the flag name specific
- test both the `true` and `false` paths
- add the flag before the incident, not during it
- remove old kill switches when they are no longer useful

## Good first kill switches

The best first kill switch candidates are the ones that would hurt most if they broke in production:

- checkout or payments
- a risky third-party integration
- a new onboarding flow
- a heavy background job
- a route or feature that should be easy to disable quickly

## Example naming patterns

- `Features:Checkout`
- `Features:PromoBanner`
- `Features:DisablePayments`
- `Features:UseLegacySearch`

The best names describe the user-facing behavior, not the internal implementation.

## Related docs

- [Add a kill switch](/docs/get-started/kill-switch/)
- [Feature flags vs remote config](/docs/feature-flags/feature-flags-vs-remote-config/)
- [History and rollback](/docs/concepts/history-and-rollback/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)

## FAQ

### What makes a kill switch different from a normal feature flag?

A kill switch is a feature flag whose main job is fast disablement under real operational pressure.

### Should a kill switch always be boolean?

Usually yes.

Boolean values are the clearest fit for a fast on/off operational control.

### What is the best first kill switch candidate?

A risky production path such as checkout, payments, onboarding, or a third-party integration is usually the best first candidate.

### Why does rollback matter for kill switches?

Because incident changes happen fast, and rollback gives you a safer way to return to a known earlier state than retyping values manually.
