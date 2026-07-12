---
title: Add a kill switch
description: Use a boolean Nona parameter as a kill switch so you can disable risky behavior without redeploying.
---

A kill switch is one of the simplest and most valuable feature flag patterns.

## Basic pattern

Create a boolean entry such as:

- key: `Features:Checkout`
- value: `true`
- scope: `client` or `all`

When something goes wrong, set it to `false`.

For backend-only behavior, use `server` scope instead.

## Why this is useful

- no app redeploy required
- one fast operational escape hatch
- easy to audit and roll back

## Good first kill switch candidates

- new checkout logic
- a risky third-party integration
- a heavy background process
- a new navigation or onboarding flow

The best kill switches guard code paths that are valuable to disable quickly under real production pressure.

## What a good kill switch does

A good kill switch should:

- be easy to understand
- default to a safe behavior when off
- be tested in both states
- be documented before the incident happens

If the application only works correctly in the `true` path, it is not really ready to benefit from a kill switch yet.

Related docs:

- [Feature flags vs remote config](/docs/feature-flags/feature-flags-vs-remote-config/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [History and rollback](/docs/concepts/history-and-rollback/)
