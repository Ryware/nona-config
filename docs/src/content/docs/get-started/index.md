---
title: Get started
description: Deploy Nona, create a project, add a parameter, create an API key, and fetch your first config value.
---

This path gets you from zero to a working Nona setup quickly.

It is designed for the smallest successful first run:

- one self-hosted Nona instance
- one project
- one environment
- one parameter or feature flag
- one API key
- one successful read

If you are evaluating the product, this path is the fastest way to understand the Nona model in practice.

## Recommended order

1. [Deploy with Docker](/docs/get-started/docker/)
2. [Create your first project](/docs/get-started/first-project/)
3. [Add your first parameter](/docs/get-started/first-parameter/)
4. [Create an API key](/docs/get-started/api-keys/)
5. [Fetch your first config value](/docs/get-started/first-api-call/)
6. [Add a kill switch](/docs/get-started/kill-switch/)

## Fastest first run

If you want the shortest possible path:

1. start Nona with Docker
2. open `/register` or `/login`
3. create a project
4. add one `boolean` parameter
5. create one API key
6. test one read with `curl`

That is enough to prove the whole runtime model end to end.

## What this path teaches

By the end of this flow, you will have touched the core Nona concepts:

- projects
- environments
- typed config entries
- scopes
- API keys
- runtime reads

That makes it the best starting point before you go deeper into:

- feature flags
- remote config architecture
- migration
- deployment topologies

## Choose your follow-up path

After the first successful setup, most teams continue into one of these:

- [Feature flags](/docs/feature-flags/) if the main use case is boolean release control
- [Remote config](/docs/remote-config/) if the main use case is runtime values and settings
- [Migration](/docs/migration/) if you are moving from Firebase Remote Config
- [Deployment](/docs/deployment/) if you are preparing a real production rollout

## What you will end up with

- one running Nona instance
- one project
- one environment
- one config entry
- one API key
- one successful read from an app or terminal

## If you prefer CLI-driven setup

After the instance is running and you have signed in once:

```bash
nona auth login --base-url https://nona.example.com
nona projects create --name storefront
nona entries set --project storefront --environment production --key Features:Checkout --value true --scope client --content-type boolean
nona keys create --project storefront --name "Web app" --scope client --environment production
```

## FAQ

### What is the shortest path to a working Nona setup?

Deploy the container, create one project, add one boolean parameter, create one API key, and verify one read.

### Do I need to understand the whole product before starting?

No.

The get-started path is designed to teach the core model while you are using it.

### Should I start with feature flags or remote config first?

Either is fine, but many teams start with one boolean flag because it is the easiest thing to verify quickly.

### What should I read after the first successful setup?

Most teams continue into feature flags, remote config, migration, or deployment depending on what they are trying to do next.
