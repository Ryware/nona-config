# Nona Config Docker Image

`rywaredev/nona` is a Docker image for **Nona Config**, a self-hosted remote configuration and feature flag service with an embedded web UI, HTTP API, and bundled libSQL database.

If you need a Firebase Remote Config alternative that you can run yourself with Docker, this image is the quickest way to start.

## Image Contents

- Nona .NET API
- Embedded web UI
- Bundled `sqld` libSQL server
- Support for standalone and primary/replica deployments

## Quick Start

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

Open `http://localhost:18080` after startup.

## Ports

| Port | Purpose |
|------|---------|
| `8080` | HTTP API and web UI |
| `9080` | libSQL HTTP endpoint |
| `5001` | gRPC replication on the primary node in primary/replica mode |

## Persistent Data

Mount `/var/lib/nona` to persist configuration data and generated JWT settings.

Example:

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

## JWT Configuration

Nona generates JWT settings automatically on first start if you do not provide them.

To set explicit JWT values:

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

## Compose Files

Production compose examples are available in the repository:

- `deploy/compose/standalone-prod.yml`
- `deploy/compose/primary-replica-prod.yml`

## Useful Links

- GitHub: [Ryware/nona-config](https://github.com/Ryware/nona-config)
- Docs: [nonaconfig.com](https://www.nonaconfig.com/)
- Creator: [ryware.dev](https://ryware.dev)
