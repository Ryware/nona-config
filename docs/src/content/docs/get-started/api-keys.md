---
title: Create an API key
description: Create a scoped Nona API key so your app can read config values safely.
---

Nona read access is driven by API keys.

An API key is bound to one project and can also be limited by:

- environment
- scope

That means an API key is not just a password. It also expresses what kind of config the app should be allowed to read.

## Pick the smallest scope that works

- use `client` for frontend and mobile apps
- use `server` for backend-only reads
- use `all` only when both are required

In practice:

- a React Native app usually starts with a `client` key
- a backend worker usually starts with a `server` key
- `all` is best reserved for cases where both sides genuinely need the same values

## Environment scoping

If possible, scope the key to the environment it actually needs.

For example:

- mobile production app -> `production`
- staging web app -> `staging`
- backend service in production -> `production`

This reduces accidental cross-environment reads and keeps access narrower.

## Recommended first key

For a simple first test:

- create a project-scoped key
- limit it to `production` if that is your target environment
- match the key scope to the config entry scope

## A simple decision guide

| App type | Suggested first scope |
|---|---|
| Web frontend | `client` |
| Mobile app | `client` |
| Backend API | `server` |
| Shared backend + frontend read path | `all` |

## Security habits

- store API keys in your secrets system or environment variables
- do not hardcode them in source code
- do not give frontend apps a broader scope than they need
- rotate keys when your access model changes
- prefer creating a new narrowly scoped key over reusing one broad key everywhere

Next: [Fetch your first config value](/docs/get-started/first-api-call/)
