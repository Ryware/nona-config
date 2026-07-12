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

## Suggested checklist

Use a checklist like this:

1. confirm the target project exists
2. confirm the expected environments exist
3. spot-check a few important keys in each environment
4. verify feature flags are still boolean
5. verify backend-only values are not accidentally client-readable
6. test one real application read path
7. test one rollback-sensitive or incident-sensitive flag
