---
title: Remote config vs environment variables
description: Compare remote config and environment variables, and understand when Nona is a better fit than shipping more values through env vars.
---

Environment variables are good for deployment-time configuration.

Remote config is better when the value should change after deployment.

## Environment variables fit when

- the value is server-only
- the value changes rarely
- updating it can safely trigger a redeploy or restart

## Remote config fits when

- product or operations teams need faster changes
- mobile or client apps need updated values without a new release
- you want one system for feature flags and dynamic settings
- the same app has multiple environments with different runtime values

## Nona-specific advantage

Nona gives you a runtime configuration system you host yourself, with:

- projects and environments
- scoped API keys
- client/server scope on entries
- history and rollback

For first implementation steps, go to [Get started](/docs/get-started/).
