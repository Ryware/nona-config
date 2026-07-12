---
title: API keys
description: Learn how Nona API keys are scoped to projects, environments, and readable config scope.
---

Nona uses API keys for config reads.

An API key belongs to one project and can optionally be restricted to:

- one environment
- one scope such as `client` or `server`

This model keeps reads narrow by default and helps you avoid overexposing config values.

## Why Nona scopes keys this way

An API key in Nona is not only an authentication token. It also represents:

- which project the app belongs to
- which environment it should read
- which scope of values it is allowed to access

That makes API keys a central part of the runtime security model.

## Practical examples

- a web frontend usually gets a `client` key
- a mobile app usually gets a `client` key
- a backend service usually gets a `server` key
- a shared read path across frontend and backend may need `all`

## Why narrower keys are better

Narrow keys reduce blast radius.

For example:

- a `client` key cannot be treated as a generic server key
- an environment-scoped key avoids accidental cross-environment reads
- a project-bound key keeps one app from reading another app's config

## Good key habits

- create separate keys for separate apps or services
- scope them as narrowly as possible
- store them in environment variables or a secrets system
- rotate them when access patterns change

## Related docs

- [Create an API key](/docs/get-started/api-keys/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
- [HTTP](/docs/clients/http/)

For a first setup flow, see [Create an API key](/docs/get-started/api-keys/).
