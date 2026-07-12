---
title: Firebase Remote Config Alternative
description: Compare Nona and Firebase Remote Config for self-hosting, open source control, HTTP access, Docker deployment, and migration paths.
---

Nona is an open source, self-hosted Firebase Remote Config alternative.

It targets the same broad problem space, but the product model is different.

## Why teams look for an alternative

Usually one or more of these:

- they want to self-host
- they want open source
- they want to avoid Google account and platform coupling
- they want plain HTTP access
- they want a migration path instead of recreating parameters by hand

## High-level differences

| Topic | Firebase Remote Config | Nona |
|---|---|---|
| Hosting | Google-hosted | Self-hosted |
| Source model | Closed | Open source |
| Setup | Firebase console and ecosystem | Docker-first service |
| Access model | SDK-heavy | Plain HTTP plus official clients |
| Migration path | N/A | Built-in CLI migration |

## What Nona emphasizes

- projects and environments
- parameter scopes: `client`, `server`, `all`
- API keys scoped to project and optionally environment
- config entry history and rollback
- parameter share links

## Migration

If you are already on Firebase, start with [Migrate from Firebase Remote Config](/migration/firebase-remote-config/).

If you want to try Nona first, go to [Get started](/get-started/).
