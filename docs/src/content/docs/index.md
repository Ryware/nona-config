---
title: Nona Docs
description: Self-hosted remote config and feature flags docs for Docker deployment, HTTP access, official clients, and Firebase migration.
---

Nona is an open source, self-hosted remote config and feature flag service for web, mobile, and backend apps.

Use Nona when you want to:

- manage runtime configuration on your own infrastructure
- ship feature flags without a hosted control plane
- expose config over plain HTTP
- separate frontend-readable and backend-only values
- migrate away from Firebase Remote Config

## Why teams use Nona

Nona is built for teams that want remote config without platform lock-in.

Core product traits:

- self-hosted
- open source
- Docker-first
- plain HTTP plus official clients
- projects and environments
- client, server, and shared scopes
- config history and rollback
- Firebase migration tooling

If you are evaluating alternatives, start with [Firebase Remote Config alternative](/docs/comparisons/firebase-remote-config-alternative/).

## Start here

- [Why Nona](/docs/why-nona/)
- [Get started](/docs/get-started/)
- [Feature flags](/docs/feature-flags/)
- [Remote config](/docs/remote-config/)
- [Firebase Remote Config alternative](/docs/comparisons/firebase-remote-config-alternative/)
- [Open source feature flags](/docs/comparisons/open-source-feature-flags/)
- [Open source remote config](/docs/comparisons/open-source-remote-config/)
- [Deployment](/docs/deployment/)
- [Migration](/docs/migration/)

## Quick path

If you want the shortest route to a working setup:

1. [Deploy with Docker](/docs/get-started/docker/)
2. [Create your first project](/docs/get-started/first-project/)
3. [Add your first parameter](/docs/get-started/first-parameter/)
4. [Create an API key](/docs/get-started/api-keys/)
5. [Fetch your first config value](/docs/get-started/first-api-call/)

## Core paths

- [Deploy with Docker](/docs/get-started/docker/)
- [Create your first project](/docs/get-started/first-project/)
- [Create an API key](/docs/get-started/api-keys/)
- [Fetch your first config value](/docs/get-started/first-api-call/)
- [HTTP client](/docs/clients/http/)
- [JavaScript client](/docs/clients/javascript/)
- [.NET client](/docs/clients/dotnet/)

## What you can do with Nona

Common remote config and feature flag use cases:

- add a kill switch for risky features
- update mobile app behavior without a store release
- separate production and staging values cleanly
- expose app settings over HTTP
- keep client-readable config separate from server-only config
- roll back a bad parameter change quickly

See [Remote config use cases](/docs/remote-config/use-cases/) for more examples.

## Feature flags

Nona is not only a remote config tool. It is also a feature flag system for teams that want:

- self-hosted flags
- kill switches
- frontend and backend flag separation
- OpenFeature support
- simple boolean rollout gates without a hosted control plane

Start here:

- [Feature flags overview](/docs/feature-flags/)
- [What are feature flags?](/docs/feature-flags/what-are-feature-flags/)
- [Feature flags vs remote config](/docs/feature-flags/feature-flags-vs-remote-config/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [Open source feature flags](/docs/comparisons/open-source-feature-flags/)
- [Self-hosted feature flags](/docs/comparisons/self-hosted-feature-flags/)
- [OpenFeature](/docs/clients/openfeature/)

## Remote config

Nona also works as a self-hosted remote config system for teams that want:

- runtime values outside deploy-time env vars
- environment-specific behavior
- client and server scope separation
- server-side remote config
- one system for config and feature flags

Start here:

- [Remote config overview](/docs/remote-config/)
- [What is remote config?](/docs/remote-config/what-is-remote-config/)
- [Remote config vs environment variables](/docs/remote-config/remote-config-vs-environment-variables/)
- [Server-side remote config](/docs/remote-config/server-side-remote-config/)
- [Open source remote config](/docs/comparisons/open-source-remote-config/)

## Key concepts

- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
- [Parameters and content types](/docs/concepts/parameters-and-content-types/)
- [Projects](/docs/concepts/projects/)
- [Environments](/docs/concepts/environments/)
- [API keys](/docs/concepts/api-keys/)
- [History and rollback](/docs/concepts/history-and-rollback/)
- [Parameter share links](/docs/parameter-share-links/)
- [Users and project access](/docs/concepts/users-and-project-access/)

## Integration paths

Use the smallest integration path that fits your app:

- [HTTP](/docs/clients/http/) for direct reads without an SDK
- [JavaScript](/docs/clients/javascript/) for Node.js and TypeScript apps
- [.NET](/docs/clients/dotnet/) for C# services and applications
- [OpenFeature](/docs/clients/openfeature/) if you want a vendor-neutral application interface
- [CLI](/docs/cli/) for admin workflows and migration work

## Migration and comparisons

If you are replacing an existing hosted setup:

- [Migrate from Firebase Remote Config](/docs/migration/firebase-remote-config/)
- [Firebase concept mapping](/docs/migration/firebase-concept-mapping/)
- [Migration validation](/docs/migration/validation/)
- [Firebase Remote Config alternative](/docs/comparisons/firebase-remote-config-alternative/)

## Production and operations

For production deployment and operations:

- [Deployment overview](/docs/deployment/)
- [Standalone production](/docs/deployment/standalone/)
- [Primary/replica production](/docs/deployment/primary-replica/)
- [Security and authentication](/docs/operations/security-and-authentication/)
- [Backups](/docs/operations/backups/)
- [Upgrades](/docs/operations/upgrades/)
- [Audit logs](/docs/concepts/audit-logs/)
- [Users and project access](/docs/concepts/users-and-project-access/)
