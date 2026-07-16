---
title: What is remote config?
description: Understand remote config, what it solves, and how Nona delivers it as a self-hosted service.
---

Remote config is a way to store application settings outside your deployed app so you can change behavior later.

Typical examples:

- enable or disable a feature
- change text or copy
- tune thresholds or limits
- switch between old and new flows
- add a kill switch for a broken feature

The key idea is that the application behavior can change without shipping a new build for every small adjustment.

In Nona, remote config is built from a few core pieces:

- a project
- one or more environments such as `development` or `production`
- config entries stored per environment
- API keys and scopes that control what can be read

That means remote config in Nona is not a vague concept. It is a concrete model your team can operate:

- create a project
- define environments
- store typed values
- read them over HTTP or an official client

## What it looks like in practice

In admin:

1. open `Projects`
2. create or open the project
3. click `Add Environment`
4. select the environment
5. click `Add Parameter`
6. choose the key, content type, and scope
7. create an API key for the runtime that will read it

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

That is remote config in its simplest operational form: define the value once, then let the app read it at runtime.

## Why teams use it

Remote config helps when you need to:

- react without a redeploy
- separate environment-specific values
- roll out behavior safely
- keep client-readable and server-only values distinct

Remote config is especially useful when:

- a mobile app should react without waiting for an app-store release
- a backend service needs operational tuning without a redeploy
- one product uses both feature flags and broader runtime values
- teams want rollback and auditability around runtime changes

## Common first values

Good first remote-config entries include:

- `App:BannerText`
- `App:MinimumSupportedVersion`
- `Limits:MaxItems`
- `App:Settings`
- `Features:Checkout`

## What makes Nona different

Nona is not a hosted control plane.

It is:

- self-hosted
- Docker-first
- open source
- usable over plain HTTP

That makes Nona a good fit for teams that want:

- self-hosted remote config
- server-side remote config
- open source feature flags and runtime settings in one product
- a model that is smaller and easier to reason about than a larger hosted platform

If you want to see where that matters, read [Firebase Remote Config alternative](/docs/comparisons/firebase-remote-config-alternative/).

## FAQ

### Is remote config only for feature flags?

No.

Feature flags are one remote-config use case, but remote config also includes text, number, and JSON values that change runtime behavior.

### What is the main benefit of remote config?

The main benefit is changing application behavior without shipping a new build for every small adjustment.

### How does Nona make remote config concrete?

Nona turns it into an explicit model of projects, environments, typed entries, scopes, and API keys instead of treating it as a vague dynamic-settings layer.

### What is a good first remote-config value?

A simple value such as `App:BannerText` or `Limits:MaxItems` is usually a good first step because it is easy to create and verify.
