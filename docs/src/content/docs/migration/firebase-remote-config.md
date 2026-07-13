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

## First command to run

Start with a dry run:

```bash
nona migrate firebase --config ./nona.migration.json --dry-run
```

That gives you the safest first look at how Firebase data will land inside Nona.

## Why the dry run matters

Use the dry run first.

It helps you verify:

- the target project name
- the expected environments
- how Firebase conditions map into Nona environments
- whether conflicting keys need to be renamed or reviewed

## Practical migration sequence

The normal operator flow is:

1. prepare the migration config file
2. run a dry run
3. review the environment and scope mapping
4. apply the migration
5. validate the imported values through the admin UI and a real runtime read

That keeps migration as a controlled cutover instead of a blind import.

## What the target should look like

A common post-migration target looks like:

- one Nona project per application boundary
- environments such as `staging` and `production`
- boolean flags stored as `boolean`
- broader settings stored as `text`, `number`, or `json`

That is the shape to validate after the import finishes.

## What to validate after import

After import, confirm:

- the expected keys exist
- boolean values came across as `boolean`
- client and server scopes match your intended read model
- critical application reads still work
- kill switches and high-risk flags behave as expected

## Apply the migration

Once the dry run looks correct:

```bash
nona migrate firebase --config ./nona.migration.json
```

Then continue immediately with [Migration validation](/docs/migration/validation/).

## Migration mindset

Treat the migration as an application cutover task, not only a data import.

The best migrations usually:

- run a dry run first
- review scope and environment mappings carefully
- validate reads from a real app or test harness
- promote production cutover only after the target behavior is confirmed

## What not to assume

Do not assume that a technically successful import means the migration is done.

The important questions are still:

- did the values land in the right environments?
- did boolean flags stay boolean?
- did server-only values remain server-readable only?
- can the real application still read what it expects?

## Detailed command docs

The CLI-specific migration guide is here:

- [CLI Firebase migration reference](/docs/cli/firebase-migration/)

## FAQ

### Should I run a dry run before importing?

Yes.

The dry run is the safest first step because it shows how Firebase data will map into Nona before you write anything to the target project.

### Do Firebase conditions stay as runtime conditions in Nona?

No.

Firebase conditions are source-side concepts for the migration flow. In Nona, they are mapped into explicit environments during import rather than preserved as a Firebase-style runtime rules engine.

### Will Firebase boolean parameters still work as feature flags?

Yes.

Boolean Firebase values map into Nona `boolean` entries, which means they continue to work naturally as feature flags after import.

### Is the migration done as soon as the import command succeeds?

No.

The import is only one part of the cutover. You still need to validate environments, scopes, content types, and real application reads before you treat the migration as complete.

Continue with:

- [Firebase concept mapping](/docs/migration/firebase-concept-mapping/)
- [Migration validation](/docs/migration/validation/)
