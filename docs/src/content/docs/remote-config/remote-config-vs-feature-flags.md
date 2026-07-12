---
title: Remote config vs feature flags
description: Learn the difference between remote config and feature flags, and how Nona supports both with the same parameter model.
---

Feature flags are usually a subset of remote config.

- A feature flag is typically a boolean switch such as `true` or `false`.
- Remote config is broader and can also include numbers, text, and JSON.

In Nona, a feature flag is just a config entry with content type `boolean`.

## Use feature flags when

- a change is on/off
- you need a kill switch
- you want a simple release gate

## Use remote config when

- a value is numeric, textual, or structured
- you need per-environment settings
- the app should read runtime settings instead of hardcoded values

## In practice

Most teams end up using both patterns together:

- `Features:Checkout` as a boolean flag
- `Checkout:BannerText` as text
- `Checkout:Settings` as JSON

Next: [Remote config vs environment variables](/remote-config/remote-config-vs-environment-variables/)
