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

## Example patterns

### Kill switch

Set `Features:Checkout` to `false` and hide or disable a failing flow.

### Mobile configuration

Serve a value like `App:MinimumSupportedVersion` from Nona instead of hardcoding it.

### Per-environment behavior

Use different values for `development`, `staging`, and `production`.

### Client and server separation

Keep frontend-readable entries scoped to `client`, and backend-only entries scoped to `server`.

For a concrete walkthrough, continue with [Get started](/docs/get-started/).
