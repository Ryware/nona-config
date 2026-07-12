---
title: Feature flags vs remote config
description: Learn the difference between feature flags and remote config, and how Nona supports both through the same project, environment, and scope model.
---

Feature flags and remote config are related, but they are not identical.

The short version:

- feature flags are usually on/off switches
- remote config is the broader category for runtime values that can also be text, number, or JSON

## The practical difference

Use feature flags when the question is:

- should this feature be on or off?
- should this route be enabled?
- should we expose this UI?
- do we need a kill switch?

Use remote config when the question is:

- what text should we show?
- what threshold should we use?
- what JSON settings should this module read?
- what value should change per environment?

## In Nona

Nona supports both with the same underlying model:

- a project contains config for one app or service
- each environment can hold different values
- each entry has a content type
- each entry has a scope

That means a team can store all of these together:

- `Features:Checkout` as `boolean`
- `Checkout:BannerText` as `text`
- `Checkout:MaxItems` as `number`
- `Checkout:Settings` as `json`

## How this looks in practice

In admin:

1. open `Projects`
2. open the project
3. select the environment
4. click `Add Parameter`
5. choose `boolean` for a flag or `text` / `number` / `json` for broader config
6. choose the right scope

With the CLI:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value true \
  --scope client \
  --content-type boolean

nona entries set \
  --project storefront \
  --environment production \
  --key Checkout:MaxItems \
  --value 50 \
  --scope server \
  --content-type number
```

## Why this model is useful

You do not need one system for feature flags and another for runtime settings.

Instead, you get:

- one deployment model
- one access model
- one audit trail
- one rollback path
- one client integration surface

## A practical rule

Ask this first:

- if the answer is on/off, start with a feature flag
- if the answer is a value, start with remote config

That avoids over-modeling simple flags as JSON and avoids forcing every runtime value into a boolean-shaped workflow.

## When feature flags should stay simple

Not every team needs a large hosted experimentation platform.

For many teams, the highest-value flag workflows are much simpler:

- boolean release gates
- kill switches
- environment-specific enablement
- frontend/backend separation through scope

That is where Nona fits best today.

## Decision guide

| Need | Better fit |
|---|---|
| Enable or disable behavior | Feature flag |
| Emergency off switch | Feature flag |
| Numeric runtime threshold | Remote config |
| Structured application settings | Remote config |
| One unified system for both | Nona |

## What to do first

If you are still deciding where to begin:

1. create one kill switch as a boolean
2. create one text or number setting
3. read both from the app
4. edit both once in admin

That gives you the clearest real-world feel for the difference.

## Related docs

- [What are feature flags?](/docs/feature-flags/what-are-feature-flags/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [Remote config vs environment variables](/docs/remote-config/remote-config-vs-environment-variables/)
