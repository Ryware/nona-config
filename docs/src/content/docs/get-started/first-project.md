---
title: Create your first project
description: Create a Nona project and environment so your app has a place to store remote config values.
---

A Nona project groups related config for one app or service.

Inside a project, you create environments such as:

- `development`
- `staging`
- `production`

Think of the project as the boundary for:

- your app's config entries
- the API keys that can read them
- the environments that separate test and production values
- the audit and rollback history for that app

## Suggested first setup

Create:

- one project for your app
- one `production` environment
- one non-production environment for testing

For many teams, a simple starting structure is:

- project: `mobile-app`
- environments: `staging`, `production`

That gives you somewhere safe to test values before they affect real traffic.

## How to think about environments

Use different environments when:

- the same key should have different values in staging and production
- you want to test a feature flag before enabling it in production
- you need a safe place to validate migrated config

Avoid creating lots of environments until you actually need them. A small, clear environment model is easier to operate.

## Why this matters

Projects and environments are the base for:

- config entry reads
- API key scoping
- migration targets
- rollback and audit history

They also give you the structure you need for both major Nona use cases:

- feature flags
- broader remote config values

## A concrete example

You might start with:

- project: `storefront`
- environment: `production`
- key: `Features:Checkout`

Later, that same project might also hold:

- `Checkout:BannerText`
- `Checkout:MaxItems`
- `Checkout:Settings`

That is why the project/environment layer comes first. Everything else builds on it.

Next: [Add your first parameter](/docs/get-started/first-parameter/)
