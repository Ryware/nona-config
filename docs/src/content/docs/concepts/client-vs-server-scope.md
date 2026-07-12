---
title: Client vs server scope
description: Understand Nona scopes so you can separate frontend-readable config from backend-only values.
---

Scope controls which kinds of consumers should be able to read an entry.

Nona supports:

- `client`
- `server`
- `all`

## Use `client` when

- the value is safe for frontend or mobile apps

## Use `server` when

- the value should stay backend-only

## Use `all` when

- both kinds of consumers need the same value

Scope also matters when you create API keys. Match key scope to the values that app should read.
