---
title: Deploy with Docker
description: Start Nona with Docker, then continue into creating a project, adding a parameter, and generating an API key for your first read.
---

For most teams, Docker is the fastest way to start Nona.

That is also one of the clearest product differences between Nona and a hosted control plane. You run the service yourself, then point your apps at it.

Nona runs as a single Docker image, so the simplest deployment path is one container with a persisted `/var/lib/nona` volume.

## Why Docker is the default starting point

Docker is the simplest way to start a self-hosted Nona instance quickly, keep the deployment model close to production, and validate the product before adding more infrastructure.

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

## Bootstrap the first flag

For the fastest non-interactive setup, use the CLI:

```bash
nona init \
  --yes \
  --base-url http://localhost:18080 \
  --email admin@example.com \
  --password <password> \
  --project storefront \
  --print-key
```

This registers or logs in the admin, creates or reuses the `storefront` project, creates or reuses the `production` environment, seeds `Features:Example=true`, creates or reuses an API key, and prints a ready-to-paste `.env` block.

`--yes` makes this safe for scripts and CI: the command never prompts and exits with an invalid-args error if a required value is missing.

If you only want to create the first admin account and save a CLI session, use the lower-level command:

```bash
nona auth register --base-url http://localhost:18080 --email admin@example.com --password <password>
```

You can also open the UI:

```text
http://localhost:18080/register
```

If the instance already has users, sign in at:

```text
http://localhost:18080/login
```

## What to click next in admin

If you used `nona init`, your first project, `production` environment, starter flag, and API key already exist. Open the admin UI when you want to inspect them or add more.

For a UI-driven setup:

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

If this works, you have already proven the most important part of the product model: Nona runs on infrastructure you control, the service is reachable, and you have a base URL for the rest of the setup flow.

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

## Docker deployment FAQ

### Do I need Docker Compose to deploy Nona?

No. The preferred first deployment path is a single Docker container.

Docker Compose is useful for local setups, team-managed environments, or when you want to use the repo's compose examples, but it is not required for the default first deployment.

### What data must persist?

Persist `/var/lib/nona`.

That volume holds the local data the container needs. If you remove the container without preserving that path, you lose the stored state.

### What should I do right after the container starts?

Use `nona init` for the shortest automated path. It creates the first account if needed, creates the first project and environment, adds a starter flag, creates an API key, and prints a verification curl.

That proves the instance is not only running, but also usable by an application.

### When should I move to the production deployment guides?

Move to the production deployment guides once you have confirmed the single-container flow works and you are ready to harden the deployment.

Use [Standalone production](/docs/deployment/standalone/) for the next operational step.

For more detailed topology and operations guidance, see:

- [Standalone production](/docs/deployment/standalone/)
- [Deployment](/docs/deployment/)

After the container is running, continue with [Create your first project](/docs/get-started/first-project/).
