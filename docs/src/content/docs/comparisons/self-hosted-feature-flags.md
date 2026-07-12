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

## Why this model is practical

Many teams do not need a large experimentation platform to get value from feature flags.

They need:

- reliable on/off switches
- narrow read scopes
- operational rollback
- auditability
- a deployment model they control

That is where Nona fits best.

## Related docs

- [Feature flags](/docs/feature-flags/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [Open source feature flags](/docs/comparisons/open-source-feature-flags/)
- [Deployment](/docs/deployment/)
