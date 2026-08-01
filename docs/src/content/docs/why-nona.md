---
title: Why Nona
description: Learn why teams choose Nona for self-hosted feature flags and remote config instead of relying on a hosted control plane.
---

Nona exists for teams that want runtime control without handing that part of the stack to a hosted platform. At a high level, it gives you self-hosted feature flags, self-hosted remote config, open source deployment and code, plain HTTP access, official JavaScript and .NET clients, and Docker-first operations.

## The short practical answer

Teams usually choose Nona because they want one product that covers feature flags, remote config, self-hosted deployment, and plain HTTP access without moving that runtime control plane into a hosted vendor dependency.

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

Nona focuses on a strong core model of projects, environments, typed config entries, scopes, API keys, history, rollback, and audit visibility. That lets teams solve common high-value problems without adopting a bigger product than they need.

## Why that model matters

For many teams, the high-value runtime use cases are not exotic: kill switches, release gates, mobile app settings, backend thresholds, and environment-specific behavior. Nona is built around solving those well with a model that stays understandable.

## Why the self-hosted model matters

Because Nona is self-hosted, your team controls where it runs, when it is upgraded, and how the surrounding infrastructure is operated. That matters for teams that want feature flags and remote config without another hosted dependency in the middle of their runtime path.

## Best fit

Nona is especially strong when you want Docker-first deployment, backend-friendly runtime config, client/server scope separation, one system for flags and non-boolean values, and migration away from Firebase Remote Config. It is less about selling a huge experimentation suite and more about doing core runtime control well.

## Feature flags and remote config together

Nona is not only a remote config system. It also supports feature flags through `boolean` entries, which means teams can use one product for kill switches, release gates, text and copy changes, numeric thresholds, and JSON settings.

## Fastest way to evaluate it

If you want to decide quickly:

1. run the Docker image
2. create one boolean flag and one text value
3. read them over HTTP
4. edit them in admin

If that flow feels right, the rest of the product model will probably fit too.

## Related docs

- [Get started](/docs/get-started)
- [Feature flags](/docs/feature-flags)
- [Remote config](/docs/remote-config)
- [OpenFeature](/docs/clients/openfeature)
- [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config)

## FAQ

### Why do teams choose Nona instead of a hosted control plane?

Usually because they want runtime control, self-hosting, open source visibility, and a smaller product model that they can operate directly.

### Is Nona only for remote config?

No.

Nona supports feature flags and broader remote config in the same system.

### What is the fastest way to evaluate Nona?

Run the Docker image, create one project, add one boolean flag and one text value, then read them over HTTP.

### What kind of team is Nona best for?

Teams that want self-hosted feature flags and remote config, plain HTTP access, and a Docker-first operating model are usually a strong fit.
