---
title: Parameters and content types
description: Learn how Nona stores parameters, which content types it supports, and how to pick the right shape for each value.
---

Nona stores config as entries keyed per environment.

Each entry has:

- a key
- a value
- a content type
- a scope

## Supported content types

- `text`
- `number`
- `boolean`
- `json`

Use `boolean` for feature flags, `json` for structured settings, and `text` or `number` for simpler runtime values.
