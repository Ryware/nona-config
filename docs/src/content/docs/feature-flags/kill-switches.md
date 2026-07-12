---
title: Kill switches
description: Use Nona kill switches to disable risky features quickly with boolean flags, scoped reads, and rollback support.
---

A kill switch is a feature flag whose main job is to turn something off fast.

This is one of the most valuable uses of Nona because it gives you an operational escape hatch without a redeploy.

## Basic pattern

Create a boolean entry such as:

- key: `Features:Checkout`
- value: `true`
- content type: `boolean`
- scope: `client`, `server`, or `all`

When the feature needs to be disabled, set the value to `false`.

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

## Operational benefits in Nona

Nona makes kill switches more useful because they fit into the rest of the product model:

- the flag lives in a project and environment
- API keys control who can read it
- history shows previous values
- rollback gives you a fast recovery path
- audit logs help explain who changed it

## Good kill switch habits

- default the application to the safest behavior when the flag is off
- keep the flag name specific
- test both the `true` and `false` paths
- add the flag before the incident, not during it
- remove old kill switches when they are no longer useful

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
