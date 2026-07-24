---
title: Environments
description: Learn how Nona environments separate development, staging, and production config values so each stage reads its own settings.
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

## Releases and active config

Each environment has one editable working configuration and zero or more immutable releases.

Public config reads use releases:

- no `version` query parameter reads the environment's active release
- `version=1.1.0` reads that exact release
- `version=1.1.x` reads the highest patch in the `1.1` line

To publish a release, open the environment's `Releases` panel and choose **Create a version**. Enter a major-minor version such as `1.1`; Nona normalizes that to `1.1.0`, opens the parameters editor loaded with the current working configuration, and lets you adjust the parameters before choosing **Create release**.

Publishing does not change what clients receive. It only creates the snapshot. Use **Activate** on a release when you are ready for it to serve clients that omit a `version`.

To patch an older line, choose **Amend** on that release. Nona automatically targets the next patch version, for example `1.1.1`, and loads a **separate, editable copy** of that release's parameters. Adjust them and choose **Create release** to publish the new patch. Amend never touches the environment's working configuration — the copy is published directly from what you edit, so you can amend an old line without disturbing the config you are preparing for the next release.

Non-active releases can be permanently deleted from the release list. Clear or replace the active release before deleting it. Deleting a release does not change the editable working configuration.

For the full release workflow, see [Releases](/docs/concepts/releases/).

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
- [Releases](/docs/concepts/releases/)
- [Create your first project](/docs/get-started/first-project/)
- [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config/)
