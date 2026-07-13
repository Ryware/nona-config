---
title: Firebase concept mapping
description: Understand how Firebase Remote Config concepts map into Nona projects, environments, scopes, and content types.
---

The important migration shift is that Nona is not Firebase with renamed screens.

The migration works best when you understand that a few concepts have to move from one mental model into another.

Nona is also not only a remote-config destination. It is a feature-flag and remote-config system, so many migrated Firebase booleans become first-class feature flags in practice.

## High-level mapping table

| Firebase concept | Nona concept | What to know |
|---|---|---|
| Firebase project data | Nona project | One Firebase source ends up inside one Nona project target. |
| Firebase conditions | Nona environments | Conditions are not evaluated dynamically by Nona. They are mapped into explicit target environments during migration. |
| Firebase namespaces | Nona scopes | The migrator can import different namespaces with `client`, `server`, or `all` scope. |
| Firebase value types | Nona content types | Firebase types are translated into `text`, `boolean`, `number`, or `json`. |
| Firebase boolean parameters | Nona boolean entries | These work naturally as feature flags after import. |

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

## Firebase project to Nona project

The migrator writes imported values into one Nona project.

That means you should choose the target project name intentionally. In most migrations, the right target is the app or service that already owns the Firebase configuration being moved.

## Conditions to environments

This is one of the biggest conceptual changes.

Firebase conditions are a source-side targeting concept. Nona environments are a target-side organization concept.

During migration, condition names can be mapped into Nona environment names through `conditionEnvironmentMappings`.

Example:

| Firebase condition | Nona environment |
|---|---|
| `production` | `production` |
| `staging` | `staging` |
| `prod-hotfix` | `production` |

This means the migration turns Firebase conditional values into explicit environment-specific entries in Nona.

That is different from treating Firebase conditions as a runtime rules engine inside Nona.

## Condition order still matters

The migrator preserves an important Firebase behavior: condition order.

For each key and target environment, it uses the first matching Firebase condition in Firebase condition order. That means you should review your Firebase condition ordering before migration if the source setup depends on precedence.

## Default values to environments

Firebase defaults do not disappear during migration. They are written into whichever Nona environments you choose in `defaultValueEnvironments`.

If `applyDefaultToMappedEnvironments` is enabled, default values can also be written into mapped environments that do not have a matching conditional value.

That makes the migration more predictable for teams that expect a fallback value in every important environment.

## Namespace to scope mapping

Firebase namespaces are not copied into Nona as namespaces.

Instead, the migrator imports each source namespace with a Nona scope:

- `client`
- `server`
- `all`

This is one of the most important product-model differences because Nona scope affects which applications and API keys can read a value after migration.

If you do not configure explicit sources, the migrator defaults to:

| Firebase namespace | Nona scope |
|---|---|
| `firebase` | `client` |
| `firebase-server` | `server` |

That default mapping fits many teams well because it separates frontend/mobile reads from backend-only reads immediately.

## Flag-specific mapping

Boolean Firebase values map naturally into Nona `boolean` entries.

That matters because these values continue to work as feature flags after import, not just as generic strings.

In practice, this usually means values such as:

- `checkout_v2`
- `new_homepage`
- `maintenance_mode`
- `kill_switch`

become Nona boolean entries that can be managed like normal feature flags.

## Content type mapping

- `STRING` -> `text`
- `BOOLEAN` -> `boolean`
- `NUMBER` -> `number`
- `JSON` -> `json`

If Firebase leaves `valueType` unspecified, the migrator falls back to `text`.

If it sees an unknown Firebase type, it also falls back to `text` and emits a warning.

## Scope mapping

The migration also supports Nona scope assignment:

- `client`
- `server`
- `all`

That is an important difference from a naive import because scope affects which apps and keys can read the migrated value.

## Parameter groups

Firebase parameter groups are flattened during migration.

The migrated Nona entry keeps the original parameter key rather than turning the group into another hierarchy layer.

That is good for preserving application reads, but it also means duplicate keys across flattened groups are a migration problem you should resolve before import.

## Conflicts across sources

When multiple Firebase sources produce the same key in the same Nona environment, the migrator does not pretend that all overlaps are harmless.

Current behavior in the repo:

- matching values can have their scopes merged
- conflicting values can be renamed with numeric suffixes when `renameConflictingKeys` is enabled
- conflicting values can also be skipped with warnings when renaming is disabled

This is one more reason to treat migration as a reviewed cutover, not just a blind copy task.

## What does not map 1:1

Do not expect a perfect one-to-one product translation.

What changes during migration:

- conditions become environment-targeted values
- namespaces become scopes
- booleans become feature-flag-friendly entries
- the target model becomes self-hosted and project/environment based

That is why the migration can succeed technically while still needing product and operational review.

## Recommended review checklist

After planning the mapping, confirm:

- the target Nona project name is correct
- important Firebase conditions map to the right Nona environments
- client/server separation is reflected in scopes
- booleans landed as `boolean`
- shared settings that should stay text or JSON kept the correct type
- conflicting keys were handled intentionally

For the command and config details, use [CLI Firebase migration reference](/docs/cli/firebase-migration/).
