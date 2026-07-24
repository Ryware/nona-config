---
title: Projects
description: Understand how Nona projects group config for an app or service and act as the boundary for keys, environments, and team access.
---

A project is the top-level container for config in Nona.

Use one project per app, service, or product boundary.

Projects are important because they define the boundary for:

- environments
- API keys
- config entries
- project access

## What belongs in one project

A good project usually represents one deployable application or service boundary.

Examples:

- one mobile app
- one web frontend
- one backend API
- one internal admin tool

## How to create a project

In admin:

1. sign in
2. open `Projects`
3. create the project if it does not exist yet
4. open the project page
5. add the environments the app needs

With the CLI:

```bash
nona projects create --name storefront
nona projects list
```

After the project exists, that becomes the boundary for later `nona entries ...` and `nona keys ...` commands.

## Why project boundaries matter

Projects are not only for organization. They also shape:

- who can access the config
- which API keys belong to which app
- which environments exist for that app
- how migration targets are defined

That means a clean project model makes everything else easier:

- onboarding
- access control
- key management
- production operations

## When to split projects

Split into multiple projects when:

- two apps should not share API keys
- environments and rollout timing differ significantly
- different teams own the config independently
- access should be isolated between products

## When not to split too early

Do not create lots of projects just because different keys exist.

If the same app and team own the values, one project with clear environments is usually better than many tiny projects.

## A good first model

For many teams, a strong first structure is:

- project: `storefront`
- environments: `staging`, `production`
- one `client` API key for the frontend
- one `server` API key for the backend

That is usually enough structure to get started without over-partitioning the system.

## FAQ

### Should one app always map to one project?

Usually yes.

One project per app or service boundary is the clearest starting model for keys, environments, and access.

### When should I split into multiple projects?

Split when apps should not share API keys, environments, ownership, or access boundaries.

### Can one project contain both feature flags and remote config?

Yes.

That is a normal and intended Nona usage pattern.

### What is the most common project mistake?

Creating too many projects too early.

If the same app and team own the values, one clear project is usually better than several tiny ones.

## Related docs

- [Environments](/docs/concepts/environments/)
- [API keys](/docs/concepts/api-keys/)
- [Users and project access](/docs/concepts/users-and-project-access/)
