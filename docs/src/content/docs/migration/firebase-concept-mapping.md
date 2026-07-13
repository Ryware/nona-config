---
title: Firebase concept mapping
description: Understand how Firebase Remote Config concepts map into Nona projects, environments, scopes, and content types.
---

The important migration shift is that Nona is not Firebase with renamed screens. The migration works best when you understand that a few concepts move from one mental model into another. Nona is also not only a remote-config destination: many migrated Firebase booleans become first-class feature flags in practice.

## High-level mapping table

| Firebase concept | Nona concept | What to know |
|---|---|---|
| Firebase project data | Nona project | One Firebase source ends up inside one Nona project target. |
| Firebase conditions | Nona environments | Conditions are not evaluated dynamically by Nona. They are mapped into explicit target environments during migration. |
| Firebase namespaces | Nona scopes | The migrator can import different namespaces with `client`, `server`, or `all` scope. |
| Firebase value types | Nona content types | Firebase types are translated into `text`, `boolean`, `number`, or `json`. |
| Firebase boolean parameters | Nona boolean entries | These work naturally as feature flags after import. |

## Why this matters

If you expect Firebase concepts to stay unchanged, the migration will feel confusing. The cleaner model is that Firebase concepts are source concepts, Nona concepts are target concepts, and the migration translates between them.

## Firebase project to Nona project

The migrator writes imported values into one Nona project, so choose the target project name intentionally. In most migrations, the right target is the app or service that already owns the Firebase configuration being moved.

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

This means the migration turns Firebase conditional values into explicit environment-specific entries in Nona instead of treating Firebase conditions as a runtime rules engine inside Nona.

## Condition order still matters

The migrator preserves an important Firebase behavior: condition order. For each key and target environment, it uses the first matching Firebase condition in Firebase condition order, so review your Firebase condition ordering before migration if the source setup depends on precedence.

## Default values to environments

Firebase defaults do not disappear during migration. They are written into whichever Nona environments you choose in `defaultValueEnvironments`. If `applyDefaultToMappedEnvironments` is enabled, default values can also be written into mapped environments that do not have a matching conditional value.

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

Boolean Firebase values map naturally into Nona `boolean` entries, so they continue to work as feature flags after import instead of only as generic strings.

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

## Parameter groups

Firebase parameter groups are flattened during migration. The migrated Nona entry keeps the original parameter key rather than turning the group into another hierarchy layer, which preserves application reads but means duplicate keys across flattened groups should be resolved before import.

## Conflicts across sources

When multiple Firebase sources produce the same key in the same Nona environment, the migrator does not pretend that all overlaps are harmless.

Current behavior in the repo:

- matching values can have their scopes merged
- conflicting values can be renamed with numeric suffixes when `renameConflictingKeys` is enabled
- conflicting values can also be skipped with warnings when renaming is disabled

This is one more reason to treat migration as a reviewed cutover, not just a blind copy task.

## What does not map 1:1

Do not expect a perfect one-to-one product translation. Conditions become environment-targeted values, namespaces become scopes, booleans become feature-flag-friendly entries, and the target model becomes self-hosted and project/environment based. That is why the migration can succeed technically while still needing product and operational review.

## Recommended review checklist

After planning the mapping, confirm:

- the target Nona project name is correct
- important Firebase conditions map to the right Nona environments
- client/server separation is reflected in scopes
- booleans landed as `boolean`
- shared settings that should stay text or JSON kept the correct type
- conflicting keys were handled intentionally

For the command and config details, use [CLI Firebase migration reference](/docs/cli/firebase-migration/).

## FAQ

### Does Firebase map one-to-one into Nona?

No.

The migration translates from Firebase source concepts into Nona target concepts rather than preserving a one-to-one product model.

### Do Firebase conditions stay as runtime targeting rules?

No.

They are mapped into explicit Nona environments during migration instead of remaining a Firebase-style runtime rules engine.

### Do Firebase boolean values stay useful after migration?

Yes.

Boolean Firebase values map naturally into Nona `boolean` entries, which means they continue to work well as feature flags.

### What is the biggest concept shift to understand before migrating?

The biggest shift is that Nona is a self-hosted project/environment/scope model, not Firebase with renamed screens.
