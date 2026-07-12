---
title: Clients and API
description: Choose between HTTP, JavaScript, .NET, and OpenFeature when integrating Nona into your application.
---

Nona can be read through several integration paths.

The right one depends on how much abstraction your application needs and what runtime you are working in.

## Integration options

- [HTTP](/docs/clients/http/) for the smallest raw request path
- [JavaScript](/docs/clients/javascript/) for Node.js, TypeScript, and related environments
- [.NET](/docs/clients/dotnet/) for C# services and applications
- [OpenFeature](/docs/clients/openfeature/) for a vendor-neutral feature-flag interface

## What to set up first

Before choosing a client, make sure the runtime can actually read something:

1. open `Projects` in the admin UI
2. create or open the project
3. create the target environment such as `production`
4. create a parameter or feature flag
5. create an API key with the right scope

If you prefer the CLI for the data setup step:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value true \
  --scope client \
  --content-type boolean

nona keys create \
  --project storefront \
  --name "Web app" \
  --scope client \
  --environment production
```

## How to choose

Choose [HTTP](/docs/clients/http/) when:

- you want the smallest possible integration
- your language does not need an official client
- you are validating reads during setup or migration

Choose [JavaScript](/docs/clients/javascript/) when:

- your application is in JavaScript or TypeScript
- you want a direct client API
- you want optional TTL cache behavior

Choose [.NET](/docs/clients/dotnet/) when:

- your application is in C#
- you want typed JSON reads
- you want built-in cache behavior

Choose [OpenFeature](/docs/clients/openfeature/) when:

- your team already uses OpenFeature
- you want a flag-oriented, vendor-neutral interface
- you want to keep application code less product-specific

## Fastest validation path

If you are still setting up the instance, validate the API key with [HTTP](/docs/clients/http/) first.

Once that works, move to [JavaScript](/docs/clients/javascript/) or [.NET](/docs/clients/dotnet/) for application code.

## Related docs

- [Get started](/docs/get-started/)
- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
- [Feature flags](/docs/feature-flags/)
- [Remote config](/docs/remote-config/)
