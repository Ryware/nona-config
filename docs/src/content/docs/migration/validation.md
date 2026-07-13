---
title: Migration validation
description: Validate a Firebase-to-Nona migration by checking environments, values, scopes, and application reads before cutover.
---

Do not treat a completed import as a finished migration.

Validate:

1. the target project exists
2. expected environments exist
3. important parameters were imported
4. scopes are correct for client and server reads
5. content types match expectations
6. your application can read the migrated values

## Why validation matters

Firebase and Nona do not expose the exact same product model.

That means a migration can succeed technically while still being wrong operationally if:

- values landed in the wrong environment
- client/server scopes are too broad or too narrow
- boolean flags became text values
- an application is still reading the wrong key or environment

## High-priority validation targets

Check these first:

- kill switches
- release-gating feature flags
- production-only values
- backend-only values
- any key tied to incidents, billing, routing, or security-sensitive behavior

## Recommended flow

- run the migration with `--dry-run`
- inspect the plan
- apply it
- test a few critical reads over HTTP or a client SDK
- verify kill switches and high-risk flags first

## CLI checks

Start with the migrator itself:

```bash
nona migrate firebase --config ./nona.migration.json --dry-run
nona migrate firebase --config ./nona.migration.json
```

Then inspect the target project with normal CLI commands:

```bash
nona entries list --project storefront --environment production
nona entries get --project storefront --environment production --key Features:Checkout
nona entries get --project storefront --environment production --key App:BannerText
```

If you need to verify that a migrated key stayed boolean:

```bash
nona entries get --project storefront --environment production --key Features:Checkout
```

Then confirm the datatype in the admin UI by opening the same parameter and checking its settings drawer. That is the safest way to verify that a feature flag stayed `boolean` instead of landing as `text`.

## Admin checks

Use the admin UI for the visual pass:

1. open `Projects`
2. open the migrated project
3. click each environment tab you mapped from Firebase
4. spot-check important parameters in the table
5. click a few migrated parameters and inspect their settings
6. open the `History` tab for high-risk parameters to confirm the import wrote the expected versions

If you migrated shareable or operationally sensitive flags, also review [Audit logs](/docs/concepts/audit-logs/) after the cutover.

## Suggested checklist

Use a checklist like this:

1. confirm the target project exists
2. confirm the expected environments exist
3. spot-check a few important keys in each environment
4. verify feature flags are still boolean
5. verify backend-only values are not accidentally client-readable
6. test one real application read path
7. test one rollback-sensitive or incident-sensitive flag

## One real read test

Do not end validation in the admin UI. Run one actual read from the same kind of app that will use the config:

```bash
curl "https://nona.example.com/api/production/Features%3ACheckout" \
  -H "X-Api-Key: <production-client-or-server-key>"
```

That final check proves the environment, key, API key scope, and public read path all line up after the migration.

## Migration validation FAQ

### Is a successful import enough to declare the migration done?

No.

A successful import only proves the write step completed. You still need to validate environments, scopes, datatypes, and real reads before production cutover.

### What should I validate first?

Start with high-risk values:

- kill switches
- release flags
- backend-only values
- production-only settings

### Should I validate only in the admin UI?

No.

The admin UI is useful for inspection, but you also need at least one real read path through HTTP or a client SDK to prove the runtime behavior is correct.

### What is the most common migration mistake to catch here?

A value landing in the wrong environment or with the wrong scope.

That kind of issue can survive a technically successful import and still break the real application behavior.
