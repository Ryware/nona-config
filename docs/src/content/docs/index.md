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

If you are evaluating alternatives, start with [Firebase Remote Config alternative](/comparisons/firebase-remote-config-alternative/).

## Start here

- [Get started](/get-started/)
- [Remote config](/remote-config/)
- [Firebase Remote Config alternative](/comparisons/firebase-remote-config-alternative/)
- [Deployment](/deployment/)
- [Migration](/migration/)

## Quick path

If you want the shortest route to a working setup:

1. [Deploy with Docker](/get-started/docker/)
2. [Create your first project](/get-started/first-project/)
3. [Add your first parameter](/get-started/first-parameter/)
4. [Create an API key](/get-started/api-keys/)
5. [Fetch your first config value](/get-started/first-api-call/)

## Core paths

- [Deploy with Docker](/get-started/docker/)
- [Create your first project](/get-started/first-project/)
- [Create an API key](/get-started/api-keys/)
- [Fetch your first config value](/get-started/first-api-call/)
- [HTTP client](/clients/http/)
- [JavaScript client](/clients/javascript/)
- [.NET client](/clients/dotnet/)

## What you can do with Nona

Common remote config and feature flag use cases:

- add a kill switch for risky features
- update mobile app behavior without a store release
- separate production and staging values cleanly
- expose app settings over HTTP
- keep client-readable config separate from server-only config
- roll back a bad parameter change quickly

See [Remote config use cases](/remote-config/use-cases/) for more examples.

## Key concepts

- [Client vs server scope](/concepts/client-vs-server-scope/)
- [Parameters and content types](/concepts/parameters-and-content-types/)
- [Projects](/concepts/projects/)
- [Environments](/concepts/environments/)
- [API keys](/concepts/api-keys/)
- [History and rollback](/concepts/history-and-rollback/)
- [Parameter share links](/parameter-share-links/)
- [Users and project access](/concepts/users-and-project-access/)

## Integration paths

Use the smallest integration path that fits your app:

- [HTTP](/clients/http/) for direct reads without an SDK
- [JavaScript](/clients/javascript/) for Node.js and TypeScript apps
- [.NET](/clients/dotnet/) for C# services and applications
- [OpenFeature](/clients/openfeature/) if you want a vendor-neutral application interface
- [CLI](/cli/) for admin workflows and migration work

## Migration and comparisons

If you are replacing an existing hosted setup:

- [Migrate from Firebase Remote Config](/migration/firebase-remote-config/)
- [Firebase concept mapping](/migration/firebase-concept-mapping/)
- [Migration validation](/migration/validation/)
- [Firebase Remote Config alternative](/comparisons/firebase-remote-config-alternative/)

## Production and operations

For production deployment and operations:

- [Deployment overview](/deployment/)
- [Standalone production](/deployment/standalone/)
- [Primary/replica production](/deployment/primary-replica/)
- [Audit logs](/concepts/audit-logs/)
- [Users and project access](/concepts/users-and-project-access/)
