---
title: Self-Hosted Feature Flags
description: Use Nona for self-hosted feature flags with Docker deployment, scoped reads, kill switches, auditability, and OpenFeature support.
---

Self-hosted feature flags are useful when your team wants runtime control without depending on a hosted flag control plane.

That usually means you care about some combination of:

- infrastructure control
- data ownership
- lower vendor coupling
- backend and frontend flag support
- simpler deployment and operating models

Nona is designed for that kind of setup.

## Why teams choose self-hosted feature flags

Common reasons:

- they do not want another hosted dependency in the request path
- they want to control where config and flag data lives
- they want feature flags and remote config in one product
- they want plain HTTP access and official clients
- they want a system that fits existing Docker-based operations

## What self-hosting looks like with Nona

Nona runs as a self-hosted service that your team deploys and operates.

That means:

- you decide where it runs
- you control upgrade timing
- you manage the surrounding infrastructure
- your applications read flags from your Nona instance

## Fastest self-hosted path

The shortest deployment path is one container:

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

Then:

1. open `/register`
2. create a project
3. add a boolean flag
4. create an API key
5. verify one read from the app or `curl`

## Flag model

In Nona, a flag is usually a config entry with:

- content type `boolean`
- scope `client`, `server`, or `all`
- one or more environment-specific values

That model supports:

- release gating
- kill switches
- mobile flags
- backend flags

## Why this matters operationally

Self-hosted flags are only useful if the runtime path stays simple.

Nona keeps that path small:

- one Docker image
- one persistent data directory
- one admin UI
- one HTTP API
- one model for flags and broader runtime config

## Why this model is practical

Many teams do not need a large experimentation platform to get value from feature flags.

They need:

- reliable on/off switches
- narrow read scopes
- operational rollback
- auditability
- a deployment model they control

That is where Nona fits best.

## Good fit checklist

Nona is a strong fit for self-hosted feature flags when you want:

- infrastructure control
- Docker-first deployment
- kill switches
- scoped frontend and backend reads
- history and rollback
- OpenFeature support without a hosted flag control plane

## FAQ

### What makes Nona self-hosted?

You deploy and operate the Nona service yourself.

That means your team controls where it runs, how it is upgraded, and how applications access it.

### Does Nona require a hosted vendor control plane?

No.

Nona is designed to run on infrastructure you control, which is exactly why it fits teams looking for self-hosted feature flags.

### Can self-hosted Nona handle kill switches?

Yes.

Boolean entries work naturally as feature flags and kill switches, and they can be scoped to the right read surface.

### Is Nona trying to be a full experimentation platform?

No.

The stronger position is that Nona provides a simpler self-hosted system for feature flags and remote config, not a giant experimentation suite.

## Related docs

- [Feature flags](/docs/feature-flags/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [Open source feature flags](/docs/comparisons/open-source-feature-flags/)
- [Deployment](/docs/deployment/)
