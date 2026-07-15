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

## How to create environments

The first automated setup path is `nona init`, which creates or reuses the environment you pass with `--env` and defaults to `production`:

```bash
nona init --yes --base-url https://nona.example.com --email admin@example.com --password <password> --project storefront --env production
```

In admin:

1. open `Projects`
2. open the project
3. click `Add Environment`
4. create `staging`
5. click `Add Environment` again
6. create `production`

Those environments then appear as selectable tabs on the project page.

Use `init` for bootstrap automation and the admin project screen for day-to-day environment management.

## Common environment models

Most teams start with:

- `staging`
- `production`

Some teams also use:

- `development`
- `preview`

The right answer depends on your release flow, but the structure should stay simple until you actually need more.

## What to store in each environment

Typical examples:

- `Features:Checkout` = `false` in `staging`, `true` in `production`
- `App:BannerText` with different copy in each environment
- `Limits:MaxItems` with safer test values outside production

This is one of the main reasons environments exist: the key names stay stable while the values change by stage.

## Good environment habits

- keep environment names predictable
- avoid creating environments that do not map to real operational stages
- test risky flags and parameters outside production first
- scope API keys to the environment they actually need when possible

## Practical environment check

After creating environments:

1. switch between the environment tabs
2. create one parameter in `staging`
3. create or edit the same key in `production`
4. verify the values differ as expected

## Firebase migration note

In the Firebase migration flow, Firebase conditions can be mapped into Nona environments during import.

That does not mean Nona should be documented as using Firebase-style live condition targeting. The migration is a bridge from one model into another.

## FAQ

### How many environments should most teams start with?

Most teams should start with `staging` and `production`.

That is enough to test safely without creating an unnecessary environment sprawl.

### Should environment names match real operational stages?

Yes.

Environment names should map to real runtime stages that your team actually uses.

### Can the same key exist in multiple environments?

Yes.

That is one of the main reasons environments exist. The key stays stable while the value changes by stage.

### Are Firebase conditions the same thing as Nona environments?

No.

Firebase conditions can be mapped into Nona environments during migration, but Nona environments are not a Firebase-style runtime targeting engine.

## Related docs

- [Projects](/docs/concepts/projects/)
- [Create your first project](/docs/get-started/first-project/)
- [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config/)
