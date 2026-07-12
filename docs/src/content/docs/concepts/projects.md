---
title: Projects
description: Understand how Nona projects group config for an app or service and act as the boundary for keys and access.
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

## Related docs

- [Environments](/docs/concepts/environments/)
- [API keys](/docs/concepts/api-keys/)
- [Users and project access](/docs/concepts/users-and-project-access/)
