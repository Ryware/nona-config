---
title: Remote config
description: Learn what remote config is, where Nona fits, and when to use it instead of hardcoded values or a hosted service.
---

Remote config lets you change application behavior without shipping a new build.

That can mean:

- flipping a feature-related value
- changing app text or copy
- tuning backend thresholds
- updating JSON settings
- separating staging and production behavior cleanly

With Nona, that model is:

- self-hosted
- open source
- plain HTTP plus official clients
- organized around projects, environments, parameters, and scopes

Use this section to understand the problem space before you pick an implementation path.

## Why remote config matters

Remote config becomes valuable when deployment-time configuration is not enough.

That usually happens when:

- a value should change after release
- mobile apps need updated settings without a new store build
- operators need faster control over production behavior
- one app needs both feature flags and non-boolean runtime settings

## How Nona approaches remote config

Nona keeps the model small and explicit:

- projects define app boundaries
- environments separate runtime stages
- entries hold typed values
- scopes control who can read those values
- API keys control application access

That makes remote config easier to reason about than a vague "dynamic settings" layer.

## Quick start for remote config

In admin:

1. open `Projects`
2. open the project
3. create or select an environment
4. click `Add Parameter`
5. create a value such as `App:BannerText` or `Limits:MaxItems`
6. create an API key for the runtime that should read it

With the CLI:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key App:BannerText \
  --value "Free shipping this week" \
  --scope client \
  --content-type text
```

## In this section

- [What is remote config?](/docs/remote-config/what-is-remote-config)
- [Feature flags vs remote config](/docs/feature-flags/feature-flags-vs-remote-config)
- [Remote config vs environment variables](/docs/remote-config/remote-config-vs-environment-variables)
- [Remote config for mobile apps](/docs/remote-config/mobile-app-remote-config)
- [Remote config use cases](/docs/remote-config/use-cases)
- [Server-side remote config](/docs/remote-config/server-side-remote-config)
- [Open source remote config](/docs/comparisons/open-source-remote-config)
- [Firebase Remote Config alternative](/docs/comparisons/firebase-remote-config-alternative)

## Next steps

- Start with [Get started](/docs/get-started)
- Compare [Nona vs Firebase Remote Config](/docs/comparisons/firebase-remote-config-alternative)
- Read [HTTP](/docs/clients/http) if you want the smallest integration path

If you want the backend-only path first, jump directly to [Server-side remote config](/docs/remote-config/server-side-remote-config).

If your main use case is flags rather than broader runtime values, continue with [Feature flags](/docs/feature-flags).

## FAQ

### Is Nona only for remote config?

No.

Nona supports remote config and feature flags in the same system, which is one of its important product differences.

### Can Nona be used for backend services?

Yes.

Nona works for backend services as well as web and mobile applications, which is why server-side remote config is a first-class docs path.

### Do I need an SDK to use Nona remote config?

No.

You can read values directly over HTTP, or use the official JavaScript and .NET clients if that fits the application better.

### When is remote config better than environment variables?

Remote config is better when values need to change after deployment, differ by environment at runtime, or support operational control without a redeploy.
