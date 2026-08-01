---
title: Firebase Remote Config Alternative
description: Compare Nona and Firebase Remote Config for self-hosting, open source control, HTTP access, Docker deployment, and migration paths.
---

Nona is an open source, self-hosted Firebase Remote Config alternative. It solves a similar class of problems, but the product model is different. If your team is evaluating Firebase Remote Config alternatives, the real question is usually not just "can this store runtime values?" It is:

- can we run it ourselves?
- can we manage feature flags as well as remote config?
- can we avoid platform lock-in?
- can we integrate it without committing to one SDK ecosystem?
- can we migrate without rebuilding everything by hand?

## Why teams look for an alternative

Usually the driver is some mix of self-hosting, open source control, avoiding Google platform coupling, keeping plain HTTP access, handling feature flags and remote config in one system, and having a migration path instead of recreating parameters by hand.

## High-level differences

| Topic | Firebase Remote Config | Nona |
|---|---|---|
| Hosting | Google-hosted | Self-hosted |
| Source model | Closed | Open source |
| Setup | Firebase console and ecosystem | Docker-first service |
| Access model | SDK-heavy | Plain HTTP plus official clients |
| Feature flags | Part of the broader remote-config workflow | First-class use case through boolean entries and OpenFeature |
| Migration path | N/A | Built-in CLI migration |

## Decision shortcuts

Nona is usually the better fit when you want:

- one Docker-deployable service you run yourself
- feature flags and remote config in the same self-hosted system
- plain HTTP reads from any language
- backend-friendly runtime config, not only mobile-SDK-centric flows
- a Firebase exit path with migration tooling

Firebase Remote Config is still the more natural fit when your team wants to stay deeply inside the broader Firebase and Google-hosted model.

## Where Nona fits best

Nona is strongest when your team wants self-hosted runtime config, self-hosted feature flags, one system for boolean flags and structured runtime values, plain HTTP access, Docker-first deployment, project and environment isolation, and rollback with auditability. It works well for web apps, mobile apps, backend services, teams moving off Firebase, and teams that want simpler infrastructure than a larger hosted feature-flag platform.

## How to try Nona first

The fastest evaluation path is:

1. run the Docker image
2. create one project and environment
3. create one boolean flag and one non-boolean value
4. read them over HTTP

If that works, you have already validated the core replacement path.

## Product model differences

Firebase Remote Config is part of a larger hosted platform. Nona is a smaller, self-hosted system you run directly.

That changes how you think about the product:

- Firebase is designed around the Firebase and Google ecosystem.
- Nona is designed around your own deployment and your own infrastructure.
- Firebase pushes you toward its SDK and console model.
- Nona lets you use plain HTTP, official clients, the CLI, or OpenFeature.

## Example mapping

A practical Nona project after migration might look like:

- project: `mobile-app`
- environments: `staging`, `production`
- `Features:Checkout` as `boolean`
- `App:BannerText` as `text`
- `App:Settings` as `json`

That is the shape to compare against your current Firebase usage, not just a one-to-one UI comparison.

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

## Migration first step

If you are evaluating migration seriously, start with a dry run:

```bash
nona migrate firebase --config ./nona.migration.json --dry-run
```

Then continue with [Migration validation](/docs/migration/validation) before production cutover.

## Migration path

If you already use Firebase Remote Config, Nona gives you a direct CLI migration path covering source namespaces, content type mapping, scope mapping, environment mapping, dry runs, and conflict handling. Start here:

- [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config)
- [Firebase concept mapping](/docs/migration/firebase-concept-mapping)
- [Migration validation](/docs/migration/validation)

## FAQ

### Is Nona open source?

Yes. Nona is open source and self-hosted, which is a major difference from Firebase Remote Config.

That matters for teams that want source visibility, infrastructure control, and a deployment model they can run directly.

### Can Nona handle both feature flags and remote config?

Yes.

In Nona, `boolean` entries work naturally as feature flags, while `text`, `number`, and `json` entries cover broader runtime configuration.

### Does Nona use the same product model as Firebase Remote Config?

No.

Nona solves a similar problem space, but it does not keep the same hosted-platform model, and it should not be explained as a one-to-one Firebase clone.

### How should I evaluate Nona as a Firebase Remote Config replacement?

Start small.

Run the Docker image, create one project and environment, add one flag and one non-boolean parameter, then test a real read. After that, use the migration docs if you are planning a full cutover.

## What to read next

If you are evaluating Nona as a replacement:

- [Get started](/docs/get-started)
- [Feature flags](/docs/feature-flags)
- [Remote config](/docs/remote-config)
- [Client vs server scope](/docs/concepts/client-vs-server-scope)
- [OpenFeature](/docs/clients/openfeature)

If you want to try Nona first, start with [Get started](/docs/get-started).
