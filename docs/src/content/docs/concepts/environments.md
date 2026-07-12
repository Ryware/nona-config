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
