---
title: Core concepts
description: "Understand the Nona model: projects, environments, typed config entries, scopes, API keys, rollback, audit logs, and team access."
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

## How to learn the model quickly

The fastest way to internalize these concepts is:

1. create one project
2. create `staging` and `production`
3. add one boolean flag and one text value
4. create one API key
5. read both values from a client or `curl`

After that, the terms in this section stop being abstract because you have already used them.

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

## FAQ

### What is the most important Nona concept to understand first?

Start with the project, environment, entry, scope, and API key model.

Once those are clear, the rest of the product becomes much easier to reason about.

### Are these concepts only for remote config?

No.

They support both major Nona use cases: feature flags and broader remote config.

### Why does Nona emphasize a small core model?

Because a smaller model is easier to operate, document, and reason about in production.

That is part of the product position, not an accident.

### What should I do if the concepts still feel abstract?

Run through one real setup flow with a project, two environments, one flag, one text value, and one API key.

That usually makes the terminology concrete very quickly.
