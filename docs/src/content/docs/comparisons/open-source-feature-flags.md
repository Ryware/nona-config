---
title: Open Source Feature Flags
description: Use Nona for open source, self-hosted feature flags with kill switches, scoped reads, OpenFeature support, and Docker-first deployment.
---

If you are looking for open source feature flags, the core requirement is usually not just "can this toggle a boolean?"

It is more often:

- can we run it ourselves?
- can we avoid SaaS lock-in?
- can we keep frontend and backend flag reads under control?
- can we add kill switches without adding another hosted dependency?
- can we use a standard interface like OpenFeature?

Nona is built for that class of problem.

## Why teams look for open source feature flags

Common reasons:

- they want to self-host
- they want control over infrastructure and data
- they want a simpler deployment model
- they want feature flags and remote config in one system
- they want to avoid coupling application logic to a hosted platform

## What Nona provides

Nona supports feature flags through its core config-entry model.

A typical flag is:

- key: `Features:Checkout`
- value: `true` or `false`
- content type: `boolean`
- scope: `client`, `server`, or `all`

That means you can use Nona for:

- frontend feature flags
- mobile feature flags
- backend feature flags
- kill switches
- environment-specific enablement

## How to try it quickly

In admin:

1. open `Projects`
2. create or open the project
3. create `staging` and `production`
4. add a boolean parameter such as `Features:Checkout`
5. create an API key

With the CLI:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value true \
  --scope client \
  --content-type boolean
```

## Why this model works

Nona keeps feature flags inside the same system as runtime configuration.

That gives you:

- one project and environment model
- one API key model
- one audit path
- one rollback path
- one deployment surface

For many teams, that is more valuable than a bigger but more complex flag platform.

## Good fit checklist

Nona is a strong fit if you want:

- open source feature flags
- self-hosted deployment
- simple kill switches
- client/server separation through scope
- OpenFeature compatibility
- one system for flags and broader runtime config

## Open source and self-hosted

Nona is:

- open source
- self-hosted
- Docker-first
- accessible through plain HTTP
- usable with official JavaScript and .NET clients
- integrated with OpenFeature

That makes it a strong fit for teams that want self-hosted feature flags without committing to a large proprietary control plane.

## Where Nona fits best

Nona is strongest when your team wants:

- reliable boolean flags
- kill switches
- simple rollout control
- backend and frontend separation through scope
- OpenFeature compatibility
- feature flags and remote config in the same product

## What Nona is not trying to be

Nona should not be described as a giant experimentation suite.

The current repo points to a simpler and more focused model:

- feature flags
- runtime config
- scopes
- history and rollback
- migration tooling

That is often exactly what teams want when they search for open source feature flags. They want control and simplicity, not more platform sprawl.

## Next practical step

If this is the page that matches your search intent, continue with:

1. [Get started](/docs/get-started/)
2. [Kill switches](/docs/feature-flags/kill-switches/)
3. [OpenFeature](/docs/clients/openfeature/)

## FAQ

### Is Nona really open source?

Yes.

Nona is open source and self-hosted, which is part of why it fits teams looking for infrastructure control instead of a hosted flag platform.

### Can I use Nona just for feature flags?

Yes.

Many teams start with boolean flags and kill switches first, then expand into broader runtime configuration later.

### Does Nona support backend and frontend flags?

Yes.

The scope model lets you separate `client`, `server`, and shared reads so the same system can support frontend, mobile, and backend use cases.

### When is Nona a better fit than a larger flag platform?

Nona is usually the better fit when you want self-hosting, simpler operations, and one product for feature flags and remote config without committing to a larger hosted control plane.

## Related docs

- [Feature flags](/docs/feature-flags/)
- [What are feature flags?](/docs/feature-flags/what-are-feature-flags/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [OpenFeature](/docs/clients/openfeature/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
