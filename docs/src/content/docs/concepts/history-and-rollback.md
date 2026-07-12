---
title: History and rollback
description: Use Nona config entry history and rollback to inspect changes and recover quickly from bad parameter updates.
---

Nona tracks config entry history and supports rollback to a previous version.

This matters when:

- a bad value reaches production
- you need to inspect who changed a parameter
- a temporary change needs to be reverted safely

Use the CLI or admin workflows to:

- inspect entry history
- select a previous version
- roll back the current value

## Why this matters in practice

History and rollback are especially important for:

- feature flags that affect a release
- kill switches that may change during an incident
- configuration values that influence pricing, thresholds, or routing
- teams with multiple people editing the same environment

## What history gives you

History helps answer questions like:

- what value did this key have before the incident?
- who changed it?
- when did it change?
- was the value, scope, or content type modified?

That makes troubleshooting much faster than trying to reconstruct changes from memory.

## What rollback gives you

Rollback turns history into an operational tool.

Instead of retyping the old value by hand, you can move the entry back to a known version. That reduces:

- typing mistakes during incidents
- uncertainty about the last known-good value
- time spent manually recreating a previous state

## Good rollback habits

- treat rollback as part of your incident plan
- verify the environment before rolling back
- document high-risk flags and parameters ahead of time
- prefer a known previous version over guessing a replacement value

## Related docs

- [Kill switches](/docs/feature-flags/kill-switches/)
- [Audit logs](/docs/concepts/audit-logs/)
- [CLI](/docs/cli/)
