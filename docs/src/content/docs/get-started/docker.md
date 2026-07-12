---
title: Deploy with Docker
description: Start Nona with Docker, then continue into project, parameter, and API key setup.
---

For most teams, Docker is the fastest way to start Nona.

That is also one of the clearest product differences between Nona and a hosted control plane. You run the service yourself, then point your apps at it.

Use the standalone production guide as the base deployment path:

- [Standalone production](/docs/deployment/standalone/)

That guide covers:

- the compose file
- exposed API port
- persistent data volume
- JWT settings
- operational commands

## Why Docker is the default starting point

Docker is the simplest way to:

- start a self-hosted Nona instance quickly
- keep the deployment model close to production
- validate the product before adding more infrastructure

For most teams, the right flow is:

1. start Nona with Docker
2. create a project and environment
3. add a parameter or feature flag
4. create an API key
5. verify a read over HTTP or a client SDK

## What this first Docker setup proves

If you can bring the service up successfully, you have already validated the most important part of the product model:

- Nona can run on infrastructure you control
- the service is reachable
- you have a base URL for the rest of the setup flow

That is why Docker is the best first step for both evaluation and real self-hosted adoption.

After the container is running, continue with [Create your first project](/docs/get-started/first-project/).
