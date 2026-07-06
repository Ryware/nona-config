---
title: Standalone production
description: Start Nona with the production standalone Docker Compose file.
---

Use the standalone compose file when one Nona instance is enough.

Compose file:

```text
deploy/compose/standalone-prod.yml
```

## Start

From the repository root:

```bash
docker compose -f deploy/compose/standalone-prod.yml up -d
```

The API is exposed on:

```text
http://localhost:18080
```

## Configure the API port

The container listens on port `8080`. `NONA_API_PORT` controls the host port.

```bash
NONA_API_PORT=8088 docker compose -f deploy/compose/standalone-prod.yml up -d
```

With that value, the API is exposed on:

```text
http://localhost:8088
```

## Persistent data

The compose file mounts the `nona-data` Docker volume at:

```text
/var/lib/nona
```

Keep this volume when upgrading the container.

## JWT settings

By default, Nona can generate and persist JWT settings. To pin them, configure the same values every time the container starts:

```yaml
environment:
  Jwt__Key: ${NONA_JWT_KEY:?set NONA_JWT_KEY}
  Jwt__Issuer: ${NONA_JWT_ISSUER:-nona}
  Jwt__Audience: ${NONA_JWT_AUDIENCE:-nona}
```

Set `NONA_JWT_KEY` from your production secret store or `.env` file.

## Operate

```bash
docker compose -f deploy/compose/standalone-prod.yml ps
docker compose -f deploy/compose/standalone-prod.yml logs -f nona
docker compose -f deploy/compose/standalone-prod.yml down
```
