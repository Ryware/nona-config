---
title: Core concepts
description: Understand the Nona model: projects, environments, typed config entries, scopes, API keys, rollback, audit logs, and team access.
---

Nona stays understandable by using a small set of core concepts.

Once you understand these, the rest of the product becomes much easier to reason about.

## The core model

At a high level, Nona works like this:

- a project represents one app or service boundary
- environments separate runtime stages like staging and production
- entries store typed values
- scopes control who should be allowed to read them
- API keys control application access

On top of that, Nona adds the operational pieces teams need in practice:

- history
- rollback
- audit logs
- user and project access
- parameter share links

## In this section

- [Projects](/docs/concepts/projects/)
- [Environments](/docs/concepts/environments/)
- [Parameters and content types](/docs/concepts/parameters-and-content-types/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
- [API keys](/docs/concepts/api-keys/)
- [History and rollback](/docs/concepts/history-and-rollback/)
- [Audit logs](/docs/concepts/audit-logs/)
- [Parameter share links](/docs/parameter-share-links/)
- [Users and project access](/docs/concepts/users-and-project-access/)

## Why these concepts matter

They shape everything else in the product:

- how apps read config
- how teams manage feature flags
- how production changes are controlled
- how access stays narrow
- how incidents are handled safely

## Related docs

- [Get started](/docs/get-started/)
- [Feature flags](/docs/feature-flags/)
- [Remote config](/docs/remote-config/)
