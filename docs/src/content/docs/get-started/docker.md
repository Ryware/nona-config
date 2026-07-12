---
title: Deploy with Docker
description: Start Nona with Docker, then continue into project, parameter, and API key setup.
---

For most teams, Docker is the fastest way to start Nona.

That is also one of the clearest product differences between Nona and a hosted control plane. You run the service yourself, then point your apps at it.

Nona runs as a single Docker image, so the simplest deployment path is one container with a persisted `/var/lib/nona` volume.

## Why Docker is the default starting point

Docker is the simplest way to:

- start a self-hosted Nona instance quickly
- keep the deployment model close to production
- validate the product before adding more infrastructure

For most teams, the right flow is:

1. start Nona with Docker
2. create the first admin account
3. create a project and environment
4. add a parameter or feature flag
5. create an API key
6. verify a read over HTTP or a client SDK

## Start the container

The preferred first deployment path is a single container:

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

The default admin UI and API base URL are:

```text
http://localhost:18080
```

This matches the repository's published quick-start deployment model: one Docker image, one HTTP port, one persistent data volume.

## When to use Docker Compose

Docker Compose is still useful, but it is not required for the normal first deployment.

Use Compose when:

- you want the repo's ready-made examples
- you want to manage environment variables through a compose file
- you are using the documented primary/replica topology
- you are running a local or team-managed setup where Compose is convenient

If you want the repo example:

```bash
docker compose -f deploy/compose/standalone-prod.yml up -d
docker compose -f deploy/compose/standalone-prod.yml ps
```

The standalone compose file still runs the same single `rywaredev/nona:latest` container.

## Create the first admin account

Open:

```text
http://localhost:18080/register
```

If the instance already has users, sign in at:

```text
http://localhost:18080/login
```

## What to click next in admin

After you sign in:

1. open `Projects`
2. create or open the project you want to configure
3. click `Add Environment`
4. create `staging` or `production`
5. click `Add Parameter`

## Basic health checks

These commands are enough for a first smoke test:

```bash
docker logs -f nona
curl http://localhost:18080/auth/first-time
```

If the UI loads and the container stays healthy, you can move on to [Create your first project](/docs/get-started/first-project/).

## What this first Docker setup proves

If you can bring the service up successfully, you have already validated the most important part of the product model:

- Nona can run on infrastructure you control
- the service is reachable
- you have a base URL for the rest of the setup flow

That is why Docker is the best first step for both evaluation and real self-hosted adoption.

## Production notes

For a real deployment, keep these two things from day one:

- a persistent Docker volume mounted to `/var/lib/nona`
- stable JWT settings if you want to pin them explicitly

Example with explicit JWT values:

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  -e Jwt__Key=<your-secret-key> \
  -e Jwt__Issuer=nona \
  -e Jwt__Audience=nona \
  rywaredev/nona:latest
```

For more detailed topology and operations guidance, see:

- [Standalone production](/docs/deployment/standalone/)
- [Deployment](/docs/deployment/)

After the container is running, continue with [Create your first project](/docs/get-started/first-project/).
