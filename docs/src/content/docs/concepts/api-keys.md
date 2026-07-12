---
title: API keys
description: Learn how Nona API keys are scoped to projects, environments, and readable config scope.
---

Nona uses API keys for config reads.

An API key belongs to one project and can optionally be restricted to:

- one environment
- one scope such as `client` or `server`

This model keeps reads narrow by default and helps you avoid overexposing config values.

For a first setup flow, see [Create an API key](/docs/get-started/api-keys/).
