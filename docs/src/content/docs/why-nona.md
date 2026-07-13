---
title: Why Nona
description: Learn why teams choose Nona for self-hosted feature flags and remote config instead of relying on a hosted control plane.
---

Nona exists for teams that want runtime control without handing that part of the stack to a hosted platform.

At a high level, Nona gives you:

- self-hosted feature flags
- self-hosted remote config
- open source deployment and code
- plain HTTP access
- official JavaScript and .NET clients
- Docker-first operations

## The short practical answer

Teams usually choose Nona because they want one product that covers:

- feature flags
- remote config
- self-hosted deployment
- plain HTTP access

without moving that runtime control plane into a hosted vendor dependency.

## Why teams look for something like Nona

Runtime configuration is often bundled with platform lock-in.

Teams start looking for alternatives when they want:

- control over infrastructure and data
- one system for flags and runtime settings
- backend-friendly access, not only mobile-SDK workflows
- a migration path away from Firebase Remote Config
- a smaller and easier-to-understand operating model

## What using Nona actually looks like

The day-to-day model is intentionally small:

1. run one Docker container
2. create a project
3. create environments such as `staging` and `production`
4. add typed config entries
5. read them over HTTP, a client SDK, or OpenFeature

That is the product shape. It is not trying to hide behind a much larger platform story.

## What Nona emphasizes

Nona focuses on a strong core model:

- projects
- environments
- typed config entries
- scopes
- API keys
- history and rollback
- audit visibility

That lets teams solve common high-value problems without adopting a bigger product than they need.

## Why that model matters

For many teams, the high-value runtime use cases are not exotic:

- kill switches
- release gates
- mobile app settings
- backend thresholds
- environment-specific behavior

Nona is built around solving those well with a model that stays understandable.

## Why the self-hosted model matters

Because Nona is self-hosted:

- your team controls where it runs
- your team controls upgrade timing
- your team controls the surrounding infrastructure
- your applications can read runtime values from a service you operate

That matters for teams that want feature flags and remote config without another hosted dependency in the middle of their runtime path.

## Best fit

Nona is especially strong when you want:

- Docker-first deployment
- backend-friendly runtime config
- client/server scope separation
- one system for flags and non-boolean values
- migration away from Firebase Remote Config

It is less about selling a huge experimentation suite and more about doing core runtime control well.

## Feature flags and remote config together

Nona is not only a remote config system.

It also supports feature flags through `boolean` entries, which means teams can use one product for:

- kill switches
- release gates
- text and copy changes
- numeric thresholds
- JSON settings

## Good fit

Nona is a good fit for teams that want:

- open source feature flags
- open source remote config
- self-hosted deployment
- plain HTTP access
- OpenFeature integration
- Firebase migration support

## Fastest way to evaluate it

If you want to decide quickly:

1. run the Docker image
2. create one boolean flag and one text value
3. read them over HTTP
4. edit them in admin

If that flow feels right, the rest of the product model will probably fit too.

## Related docs

- [Get started](/docs/get-started/)
- [Feature flags](/docs/feature-flags/)
- [Remote config](/docs/remote-config/)
- [OpenFeature](/docs/clients/openfeature/)
- [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config/)
