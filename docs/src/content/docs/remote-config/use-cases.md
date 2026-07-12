---
title: Remote config use cases
description: See common Nona use cases including kill switches, mobile configuration, staged settings, and environment-specific app behavior.
---

Common Nona use cases:

- kill switches for unstable features
- mobile app settings without a store release
- frontend flags for incomplete UI paths
- backend thresholds and operational limits
- environment-specific configuration
- structured JSON settings for app modules

These examples matter because they show where remote config becomes more useful than hardcoded values or deploy-time settings alone.

## Example patterns

### Kill switch

Set `Features:Checkout` to `false` and hide or disable a failing flow.

This is one of the fastest ways to reduce risk in production without waiting for another deploy.

### Mobile configuration

Serve a value like `App:MinimumSupportedVersion` from Nona instead of hardcoding it.

This is useful when a mobile app needs updated behavior or messaging without going back through the app-store release cycle.

### Per-environment behavior

Use different values for `development`, `staging`, and `production`.

That keeps testing and rollout safer because one key can exist across environments without forcing all of them to share the same value.

### Client and server separation

Keep frontend-readable entries scoped to `client`, and backend-only entries scoped to `server`.

This is one of the practical differences between using Nona as a runtime system and just storing values in a generic key/value store.

### Structured app settings

Store JSON settings such as:

- app module options
- UI configuration blocks
- grouped service behavior settings

This works well when several related values naturally belong together in one object.

## Related docs

- [Server-side remote config](/docs/remote-config/server-side-remote-config/)
- [Feature flags](/docs/feature-flags/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)

For a concrete walkthrough, continue with [Get started](/docs/get-started/).
