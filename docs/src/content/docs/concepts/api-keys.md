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

## How to create one

In admin:

1. open `Projects`
2. open the project
3. use the `API Keys` section
4. enter a key name
5. choose the scope
6. optionally choose one environment
7. click `Create`

With the CLI:

```bash
nona keys create \
  --project storefront \
  --name "Backend worker" \
  --scope server \
  --environment production
```

List keys later with:

```bash
nona keys list --project storefront
```

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

## Common operating pattern

A typical production workflow looks like this:

1. create one project per app or service
2. create one key per deployable runtime
3. keep frontend keys on `client`
4. keep backend-only values behind `server`
5. test a real read before shipping the key to the app

## FAQ

### Does an API key belong to one project?

Yes.

An API key is bound to one project and can also be narrowed by environment and scope.

### Should I create one key per app or service?

Usually yes.

Separate runtimes should usually get separate keys so access stays narrower and easier to reason about.

### Should frontend keys use `client` scope?

Yes, in most cases.

Frontend and mobile apps should usually use `client` scope unless there is a real need for broader access.

### What is the most common API key mistake?

Using keys that are broader than they need to be.

That increases blast radius and makes accidental exposure harder to contain.

## Related docs

- [Create an API key](/docs/get-started/api-keys/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
- [HTTP](/docs/clients/http/)

For a first setup flow, see [Create an API key](/docs/get-started/api-keys/).
