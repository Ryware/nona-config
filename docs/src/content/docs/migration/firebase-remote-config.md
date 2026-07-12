---
title: Migrate from Firebase Remote Config
description: Use the Nona CLI to import Firebase Remote Config data into Nona with environment mapping, scope mapping, and dry runs.
---

Nona includes a built-in Firebase migration command.

Use it when you want to:

- leave a hosted control plane
- preserve existing parameter work
- import values into projects and environments you run yourself

This is one of Nona's most important product paths because moving off a hosted config platform is usually less about exporting data and more about preserving operational behavior.

## What migration actually means here

The migration is not only copying values.

It also means translating Firebase concepts into the Nona model:

- projects
- environments
- scopes
- content types
- feature flags as boolean entries

That is why the migration flow matters even for teams that only use a small part of Firebase Remote Config today.

## What the migration handles

- Firebase source namespaces
- scope mapping into `client`, `server`, or `all`
- condition-to-environment mapping
- content type conversion
- dry-run planning
- conflict handling

## Why the dry run matters

Use the dry run first.

It helps you verify:

- the target project name
- the expected environments
- how Firebase conditions map into Nona environments
- whether conflicting keys need to be renamed or reviewed

That is much safer than importing directly into a live production target.

## What to validate after import

After import, confirm:

- the expected keys exist
- boolean values came across as `boolean`
- client and server scopes match your intended read model
- critical application reads still work
- kill switches and high-risk flags behave as expected

## Migration mindset

Treat the migration as an application cutover task, not only a data import.

The best migrations usually:

- run a dry run first
- review scope and environment mappings carefully
- validate reads from a real app or test harness
- promote production cutover only after the target behavior is confirmed

## Detailed command docs

The CLI-specific migration guide is here:

- [CLI Firebase migration reference](/docs/cli/firebase-migration/)

Continue with:

- [Firebase concept mapping](/docs/migration/firebase-concept-mapping/)
- [Migration validation](/docs/migration/validation/)
