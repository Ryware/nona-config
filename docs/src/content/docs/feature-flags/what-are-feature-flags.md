---
title: What are feature flags?
description: Understand feature flags, when to use them, and how Nona supports self-hosted flags for web, mobile, and backend apps.
---

Feature flags are runtime switches that let you turn behavior on or off without redeploying an application.

The simplest feature flag is a boolean value:

- `true` means the behavior is enabled
- `false` means the behavior is disabled

## Why teams use feature flags

Feature flags are useful when you need to:

- release code before it is visible to everyone
- disable a broken feature quickly
- test behavior in non-production environments
- keep frontend and backend enablement in sync
- ship risky code with a fast rollback path

## What feature flags are not

Feature flags are not the same thing as:

- deployment configuration
- secrets management
- full experimentation platforms
- analytics-driven personalization systems

Nona focuses on the strong, reliable core:

- self-hosted flags
- scoped reads
- history and rollback
- plain HTTP and official clients

## Feature flags in Nona

In Nona, a feature flag is just a config entry with content type `boolean`.

That model gives you a few practical advantages:

- no separate flag system to learn
- the same projects and environments model as the rest of your runtime config
- the same scopes and API keys model for controlling reads
- the same audit and rollback behavior as other config entries

## Good flag examples

- `Features:Checkout`
- `Features:NewNavigation`
- `Features:PromoBanner`
- `Features:DisablePayments`

Keep names descriptive. Most teams do better with names that explain the behavior, not the implementation detail.

## Next steps

- [Feature flags vs remote config](/docs/feature-flags/feature-flags-vs-remote-config/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
