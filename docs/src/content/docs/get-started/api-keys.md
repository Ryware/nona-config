---
title: Create an API key
description: Create a scoped Nona API key so your app can read config values safely.
---

Nona read access is driven by API keys.

An API key is bound to one project and can also be limited by:

- environment
- scope

## Pick the smallest scope that works

- use `client` for frontend and mobile apps
- use `server` for backend-only reads
- use `all` only when both are required

## Recommended first key

For a simple first test:

- create a project-scoped key
- limit it to `production` if that is your target environment
- match the key scope to the config entry scope

Next: [Fetch your first config value](/get-started/first-api-call/)
