---
title: Firebase concept mapping
description: Understand how Firebase Remote Config concepts map into Nona projects, environments, scopes, and content types.
---

The important migration shift is that Nona is not Firebase with renamed screens.

The migration works best when you understand that a few concepts have to move from one mental model into another.

## Practical mappings

- Firebase project data moves into a Nona project
- Firebase conditions can be mapped into Nona environments during migration
- Firebase value types map into Nona content types
- Firebase namespaces can be imported with explicit Nona scopes

## Why this matters

If you expect Firebase concepts to stay unchanged, the migration will feel confusing.

The cleaner way to think about it is:

- Firebase concepts are source concepts
- Nona concepts are target concepts
- the migration translates between them

## Flag-specific mapping

Boolean Firebase values map naturally into Nona `boolean` entries.

That matters because these values continue to work as feature flags after import, not just as generic strings.

## Content type mapping

- `STRING` -> `text`
- `BOOLEAN` -> `boolean`
- `NUMBER` -> `number`
- `JSON` -> `json`

## Scope mapping

The migration also supports Nona scope assignment:

- `client`
- `server`
- `all`

That is an important difference from a naive import because scope affects which apps and keys can read the migrated value.

For the command and config details, use [CLI Firebase migration reference](/docs/cli/firebase-migration/).
