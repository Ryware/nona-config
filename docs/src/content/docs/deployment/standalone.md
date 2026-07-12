---
title: Standalone production
description: Run Nona as one standalone Docker container with persistent storage.
---

Use standalone when one Nona instance is enough.

For most teams, standalone is the right production starting point.

It is a good fit when you want:

- one self-hosted Nona instance
- the simplest deployment model
- one Docker-first service with persistent local data
- a straightforward place to start before introducing replication

## Start

The simplest standalone deployment is one Docker container:

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

The API and admin UI are exposed on:

```text
http://localhost:18080
```

## Compose example

If you want the repo's compose example for the same standalone image:

```bash
docker compose -f deploy/compose/standalone-prod.yml up -d
```

Compose file:

```text
deploy/compose/standalone-prod.yml
```

## When standalone is the right choice

Choose standalone when:

- one instance is operationally sufficient
- your traffic profile does not require replica reads
- simplicity matters more than distributed read scaling
- you want to validate the product in production without extra topology

For many teams, standalone will stay the long-term deployment shape, not just the first step.

## Configure the API port

The container listens on port `8080`.

With plain Docker:

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 8088:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

With Compose, `NONA_API_PORT` controls the host port:

```bash
NONA_API_PORT=8088 docker compose -f deploy/compose/standalone-prod.yml up -d
```

With that value, the API is exposed on:

```text
http://localhost:8088
```

## Persistent data

Mount a persistent volume at:

```text
/var/lib/nona
```

Keep this volume when upgrading the container.

The mounted volume is what makes the deployment durable across restarts and upgrades, so it should be treated as production data.

## JWT settings

By default, Nona can generate and persist JWT settings. To pin them, pass the same values every time the container starts.

Example with plain Docker:

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

Equivalent Compose environment block:

```yaml
environment:
  Jwt__Key: ${NONA_JWT_KEY:?set NONA_JWT_KEY}
  Jwt__Issuer: ${NONA_JWT_ISSUER:-nona}
  Jwt__Audience: ${NONA_JWT_AUDIENCE:-nona}
```

Set `NONA_JWT_KEY` from your production secret store or `.env` file.

## Operate

```bash
docker ps
docker logs -f nona
docker stop nona
```

If you are using Compose instead:

```bash
docker compose -f deploy/compose/standalone-prod.yml ps
docker compose -f deploy/compose/standalone-prod.yml logs -f nona
docker compose -f deploy/compose/standalone-prod.yml down
```

## Related docs

- [Deployment overview](/docs/deployment/)
- [Primary/replica production](/docs/deployment/primary-replica/)
- [Get started](/docs/get-started/)
