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

For Nona specifically, that also means a deliberate boundary:

- no built-in per-user targeting
- no runtime segments or cohorts
- no percentage rollout evaluator on the read path

## Feature flags in Nona

In Nona, a feature flag is just a config entry with content type `boolean`.

That model gives you a few practical advantages:

- no separate flag system to learn
- the same projects and environments model as the rest of your runtime config
- the same scopes and API keys model for controlling reads
- the same audit and rollback behavior as other config entries

## How to create a flag

In admin:

1. open `Projects`
2. open the project
3. select the environment such as `staging` or `production`
4. click `Add Parameter`
5. choose a key such as `Features:Checkout`
6. set the content type to `boolean`
7. choose the right scope
8. click `Create`

With the CLI:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value true \
  --scope client \
  --content-type boolean
```

To turn it off later:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value false \
  --scope client \
  --content-type boolean
```

## Good flag examples

- `Features:Checkout`
- `Features:NewNavigation`
- `Features:PromoBanner`
- `Features:DisablePayments`

Keep names descriptive. Most teams do better with names that explain the behavior, not the implementation detail.

## How to verify it works

After creating the flag:

1. read it once from the app, client SDK, or [HTTP](/docs/clients/http/)
2. flip the value in admin
3. confirm the application behavior changes as expected
4. check `History` if you want to verify the change timeline

## Next steps

- [Feature flags vs remote config](/docs/feature-flags/feature-flags-vs-remote-config/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)

## FAQ

### Are feature flags only for frontend releases?

No.

Feature flags are useful for frontend, mobile, and backend behavior, which is why Nona documents all three use cases.

### How do feature flags work in Nona?

In Nona, a feature flag is usually a `boolean` config entry.

That keeps the model simple and aligned with the same project, environment, scope, and API key system as the rest of the product.

### Can I roll a flag out to 10 percent of users in Nona?

No.

Nona does not provide built-in percentage rollout, beta-cohort targeting, or per-user evaluation. The built-in model is a direct environment-and-key lookup.

### Are feature flags the same as remote config?

Not exactly.

Feature flags are one important type of runtime config, but remote config is broader and also includes text, number, and JSON values.

### What is the best first feature flag to create?

A simple boolean key such as `Features:Checkout` is usually the best first choice because it is easy to create, read, and flip.
