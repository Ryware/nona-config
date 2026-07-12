---
title: Firebase Remote Config Alternative
description: Compare Nona and Firebase Remote Config for self-hosting, open source control, HTTP access, Docker deployment, and migration paths.
---

Nona is an open source, self-hosted Firebase Remote Config alternative.

It solves a similar class of problems, but the product model is different.

If your team is evaluating Firebase Remote Config alternatives, the real question is usually not just "can this store runtime values?" It is:

- can we run it ourselves?
- can we manage feature flags as well as remote config?
- can we avoid platform lock-in?
- can we integrate it without committing to one SDK ecosystem?
- can we migrate without rebuilding everything by hand?

## Why teams look for an alternative

Usually one or more of these:

- they want to self-host
- they want open source
- they want to avoid Google account and platform coupling
- they want plain HTTP access
- they want self-hosted feature flags as well as remote config
- they want a migration path instead of recreating parameters by hand

## High-level differences

| Topic | Firebase Remote Config | Nona |
|---|---|---|
| Hosting | Google-hosted | Self-hosted |
| Source model | Closed | Open source |
| Setup | Firebase console and ecosystem | Docker-first service |
| Access model | SDK-heavy | Plain HTTP plus official clients |
| Feature flags | Part of the broader remote-config workflow | First-class use case through boolean entries and OpenFeature |
| Migration path | N/A | Built-in CLI migration |

## Where Nona fits best

Nona is strongest when your team wants:

- self-hosted runtime config
- self-hosted feature flags
- one system for boolean flags and structured runtime values
- plain HTTP access
- Docker-first deployment
- project and environment isolation
- rollback and auditability

Nona is a good fit for:

- web apps
- mobile apps
- backend services
- teams moving off Firebase
- teams that want simpler infrastructure than a large hosted feature-flag platform

## What Nona emphasizes

- projects and environments
- feature flags and kill switches
- parameter scopes: `client`, `server`, `all`
- API keys scoped to project and optionally environment
- config entry history and rollback
- parameter share links
- OpenFeature support for flag-oriented integrations

## Product model differences

Firebase Remote Config is part of a larger hosted platform. Nona is a smaller, self-hosted system you run directly.

That changes how you think about the product:

- Firebase is designed around the Firebase and Google ecosystem.
- Nona is designed around your own deployment and your own infrastructure.
- Firebase pushes you toward its SDK and console model.
- Nona lets you use plain HTTP, official clients, the CLI, or OpenFeature.

## Remote config and feature flags in one system

Nona is not only about replacing runtime config reads.

It also supports feature flags through the same model:

- `boolean` entries work as flags
- `text`, `number`, and `json` entries work as broader remote config
- the same project, environment, scope, and API key model applies to both

This is useful when you do not want:

- one product for feature flags
- another product for runtime config
- a third layer for deployment-specific values

## What Nona does not try to be

Nona should not be sold as "Firebase, but self-hosted."

It is better described as:

- open source
- self-hosted
- Docker-first
- strong on core runtime config and feature flags

The current repo does not present Nona as a full experimentation, personalization, or analytics-targeting platform. That is an important product distinction.

## Migration path

If you already use Firebase Remote Config, Nona gives you a direct migration path through the CLI.

The migration docs cover:

- source namespaces
- content type mapping
- scope mapping
- environment mapping
- dry runs
- conflict handling

Start here:

- [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config/)
- [Firebase concept mapping](/docs/migration/firebase-concept-mapping/)
- [Migration validation](/docs/migration/validation/)

## What to read next

If you are evaluating Nona as a replacement:

- [Get started](/docs/get-started/)
- [Feature flags](/docs/feature-flags/)
- [Remote config](/docs/remote-config/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
- [OpenFeature](/docs/clients/openfeature/)

If you want to try Nona first, start with [Get started](/docs/get-started/).
