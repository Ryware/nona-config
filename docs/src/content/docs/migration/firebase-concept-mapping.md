---
title: Firebase concept mapping
description: Understand how Firebase Remote Config concepts map into Nona projects, environments, scopes, and content types.
---

The important migration shift is that Nona is not Firebase with renamed screens.

## Practical mappings

- Firebase project data moves into a Nona project
- Firebase conditions can be mapped into Nona environments during migration
- Firebase value types map into Nona content types
- Firebase namespaces can be imported with explicit Nona scopes

## Content type mapping

- `STRING` -> `text`
- `BOOLEAN` -> `boolean`
- `NUMBER` -> `number`
- `JSON` -> `json`

For the command and config details, use [CLI Firebase migration reference](/docs/cli/firebase-migration/).
