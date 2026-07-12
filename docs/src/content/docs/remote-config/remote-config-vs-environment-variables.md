---
title: Remote config vs environment variables
description: Compare remote config and environment variables, and understand when Nona is a better fit than shipping more values through env vars.
---

Environment variables are good for deployment-time configuration.

Remote config is better when the value should change after deployment.

These two approaches are not enemies. Most teams use both. The question is which values belong in which layer.

## Environment variables fit when

- the value is server-only
- the value changes rarely
- updating it can safely trigger a redeploy or restart
- the value belongs to infrastructure or deployment wiring

## Remote config fits when

- product or operations teams need faster changes
- mobile or client apps need updated values without a new release
- you want one system for feature flags and dynamic settings
- the same app has multiple environments with different runtime values

## A practical way to split them

Use environment variables for things like:

- connection strings
- secret references
- service wiring
- deployment-specific infrastructure settings

Use remote config for things like:

- feature flags
- copy or text values
- numeric thresholds
- JSON settings
- runtime behavior that may change after deploy

## Why teams outgrow env vars for runtime behavior

Environment variables become awkward when:

- a mobile app needs updated values
- multiple apps should read the same runtime setting
- operations wants a kill switch
- the same key should vary by environment without a redeploy
- rollback history matters

At that point, remote config is usually the cleaner model.

## Nona-specific advantage

Nona gives you a runtime configuration system you host yourself, with:

- projects and environments
- scoped API keys
- client/server scope on entries
- history and rollback

That means Nona can sit beside your deployment-time configuration instead of trying to replace it entirely.

For first implementation steps, go to [Get started](/docs/get-started/).
