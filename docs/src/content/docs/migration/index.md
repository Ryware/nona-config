---
title: Migration
description: Plan and validate a move from Firebase Remote Config to Nona using the built-in CLI migration flow, with mapping and dry runs.
---

Migration is a first-class Nona workflow.

The built-in CLI migrator helps you move Firebase Remote Config parameters into:

- a Nona project
- one or more Nona environments
- matching Nona scopes and content types

This is important because moving off Firebase Remote Config is usually not just a data-export task. It is a product-model transition:

- from a hosted control plane
- into a self-hosted runtime config and feature-flag system

## When to use this section

Use this migration section when:

- you already have Firebase Remote Config in production
- you want to preserve existing parameter work
- you need a structured cutover path
- you need to understand how Firebase concepts translate into Nona concepts

## What the migration path covers

The migration docs help you reason about:

- how Firebase concepts map into Nona concepts
- how values land in environments
- how scopes are assigned
- how boolean flags remain feature flags after import
- how to validate the result before cutover

## First command to run

If you are already at the evaluation stage, start with:

```bash
nona migrate firebase --config ./nona.migration.json --dry-run
```

Then continue with [Migration validation](/docs/migration/validation/) before production cutover.

## Migration mindset

The safest way to approach migration is:

1. understand the mapping
2. run a dry run
3. import into a controlled target
4. validate real reads
5. only then treat the new system as production-ready

## Recommended order

1. [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config/)
2. [Firebase concept mapping](/docs/migration/firebase-concept-mapping/)
3. [Migration validation](/docs/migration/validation/)

## Recommended migration sequence

1. map the Firebase concepts into Nona's model
2. prepare the migration config file
3. run a dry run
4. apply the migration
5. validate the imported environments and keys

That keeps migration as an operator workflow instead of an ad hoc manual rewrite.

## Start here

- [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config/)
- [Firebase concept mapping](/docs/migration/firebase-concept-mapping/)
- [Migration validation](/docs/migration/validation/)

## FAQ

### Is migration just an export and import task?

No.

For Nona, migration is also a model transition from Firebase concepts into projects, environments, scopes, and typed entries.

### What is the first migration command I should run?

Start with a dry run:

`nona migrate firebase --config ./nona.migration.json --dry-run`

That gives you the safest first look at how the source data will land in Nona.

### Do Firebase boolean parameters stay useful after migration?

Yes.

Boolean Firebase values map naturally into Nona boolean entries, which means they continue to work as feature flags after import.

### When is the migration actually complete?

Only after you validate environments, scopes, datatypes, and real application reads, not just after the import command succeeds.
