---
title: Add a kill switch
description: Use a boolean Nona parameter as a kill switch so you can disable risky behavior without redeploying.
---

A kill switch is one of the simplest and most valuable remote config patterns.

## Basic pattern

Create a boolean entry such as:

- key: `Features:Checkout`
- value: `true`
- scope: `client` or `all`

When something goes wrong, set it to `false`.

## Why this is useful

- no app redeploy required
- one fast operational escape hatch
- easy to audit and roll back

Related docs:

- [Remote config vs feature flags](/docs/remote-config/remote-config-vs-feature-flags/)
- [History and rollback](/docs/concepts/history-and-rollback/)
