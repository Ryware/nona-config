---
title: Environments
description: Learn how Nona environments separate development, staging, and production config values.
---

An environment stores a set of config entries for a particular runtime stage.

Typical examples:

- `development`
- `staging`
- `production`

This lets the same key exist with different values per environment.

That matters for:

- safe testing
- staged rollout preparation
- migration mapping from Firebase conditions into Nona environments

## Why environments matter

Environments let one application keep different runtime behavior without changing key names.

For example:

- `Features:Checkout` can be `false` in `staging`
- `Features:Checkout` can be `true` in `production`

That same pattern also works for non-boolean config:

- text copy
- numeric thresholds
- JSON settings

## Common environment models

Most teams start with:

- `staging`
- `production`

Some teams also use:

- `development`
- `preview`

The right answer depends on your release flow, but the structure should stay simple until you actually need more.

## Good environment habits

- keep environment names predictable
- avoid creating environments that do not map to real operational stages
- test risky flags and parameters outside production first
- scope API keys to the environment they actually need when possible

## Firebase migration note

In the Firebase migration flow, Firebase conditions can be mapped into Nona environments during import.

That does not mean Nona should be documented as using Firebase-style live condition targeting. The migration is a bridge from one model into another.

## Related docs

- [Projects](/docs/concepts/projects/)
- [Create your first project](/docs/get-started/first-project/)
- [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config/)
