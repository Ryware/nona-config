# Nona Config: Self-Hosted Remote Configuration and Feature Flags

Nona Config is a self-hosted remote configuration service and feature flag backend for web, mobile, and server applications. It provides a web UI, an HTTP API, Docker deployment, and embedded [libSQL](https://github.com/tursodatabase/libsql), so you can run remote config without depending on a hosted control plane or a separate database.

If you are looking for a Firebase Remote Config alternative that you can host yourself, Nona is built for that workflow.

> Created by [Ryware.dev](https://ryware.dev) - Full documentation at [nonaconfig.com](https://www.nonaconfig.com/)

## Why Nona

- Self-hosted remote configuration with no external database required
- Web UI and HTTP API in the same deployment
- Embedded libSQL server bundled in the container image
- Project, environment, and scoped config entries for client and server use cases
- Docker-first deployment with standalone and primary/replica topologies

## Docker Image

Production container image: `rywaredev/nona:latest`

The standalone container exposes:

| Port | Purpose |
|------|---------|
| `8080` | HTTP API and embedded web UI |
| `9080` | libSQL HTTP endpoint for primary/replica communication |

In the primary/replica setup the primary additionally exposes port `5001` for gRPC replication.

Persistent data is stored in `/var/lib/nona`. Mount this path as a Docker volume so the database survives container restarts.

## Quick Start with Docker

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

Once the container is running:

- Web UI: `http://localhost:18080`
- API base URL: `http://localhost:18080`

## JWT Settings

Nona generates and persists JWT settings automatically on first start when `Jwt__Key`, `Jwt__Issuer`, and `Jwt__Audience` are not provided. To pin your own values, pass all three:

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

## Docker Compose: Standalone

Copy [`deploy/compose/standalone-prod.yml`](deploy/compose/standalone-prod.yml) to your server and rename it to `docker-compose.yml`.

```bash
docker compose up -d
```

Default host port: `http://localhost:18080`

Optional environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `NONA_API_PORT` | `18080` | Host port mapped to the API |
| `Jwt__Key` | auto-generated | JWT signing key |
| `Jwt__Issuer` | `nona` | JWT issuer claim |
| `Jwt__Audience` | `nona` | JWT audience claim |

## Docker Compose: Primary / Replica

A primary/replica setup runs two Nona containers that share the same libSQL dataset. Reads can be served locally from the replica while writes continue to go to the primary.

Copy [`deploy/compose/primary-replica-prod.yml`](deploy/compose/primary-replica-prod.yml) to your server and rename it to `docker-compose.yml`.

```bash
docker compose up -d
```

Default host ports:

| Service | API port | libSQL port | gRPC port |
|---------|----------|-------------|-----------|
| `nona-primary` | `18081` | `19080` | `15001` |
| `nona-replica` | `18082` | `19082` | - |

Override the ports via environment variables:

| Variable | Default |
|----------|---------|
| `NONA_PRIMARY_API_PORT` | `18081` |
| `NONA_PRIMARY_LIBSQL_PORT` | `19080` |
| `NONA_PRIMARY_GRPC_PORT` | `15001` |
| `NONA_REPLICA_API_PORT` | `18082` |
| `NONA_REPLICA_LIBSQL_PORT` | `19082` |

The replica connects to the primary over gRPC and syncs automatically. No additional configuration is required beyond the compose file.

## Use Cases

- Centralized app configuration across multiple environments
- Self-hosted feature flags and runtime toggles
- Remote config for mobile or desktop apps
- API-delivered configuration for backend services
- Teams that want a Firebase Remote Config alternative under their own control

## Performance

All measurements are from production-equivalent environments.

### SQLite vs libSQL (local)

Dataset: **10,000 rows** (medium). Results measured at p95 latency.

| Scenario | SQLite p95 (ms) | SQLite rps | libSQL p95 (ms) | libSQL rps |
|----------|-----------------|------------|-----------------|------------|
| Point lookup - 1 key, c1 | 0.088 | 18,226 | 0.154 | 11,248 |
| Point lookup - 1 key, c50 | 0.093 | 17,929 | 0.142 | 11,943 |
| Point lookup - 10 keys, c10 | 0.132 | 13,703 | 0.209 | 7,287 |
| Point lookup - 100 keys, c10 | 0.276 | 4,625 | 0.712 | 1,616 |
| Range query - 1,000 rows, c10 | 0.562 | 2,407 | 3.703 | 289 |
| Range query - 10,000 rows, c5 | 2.776 | 386 | 70.107 | 24 |

Target compliance (libSQL local):

| Target | Threshold | Result |
|--------|-----------|--------|
| Single key lookup p95 | <= 80 ms | Pass: 0.154 ms |
| 100-row query p95 | <= 120 ms | Pass: 0.756 ms |
| 1,000-row query p95 | <= 750 ms | Pass: 3.703 ms |
| Error rate under load | < 2% | Pass: 0.000% |
| No degradation at 50 users | n/a | Pass: 0.142 ms |

Summary: libSQL local passes all required targets with zero errors. SQLite is faster in absolute terms, but the gap is minor for small point lookups and more material for large range queries.

### Read Replica Performance

All values measured from a secondary server against a geographically separated primary and a co-located replica.

#### Read Performance (p50 / p95 / p99 / throughput)

| Scenario | Primary p50 | Primary p95 | Primary p99 | Primary rps | Replica p50 | Replica p95 | Replica p99 | Replica rps |
|----------|-------------|-------------|-------------|-------------|-------------|-------------|-------------|-------------|
| key-1-c1 | 38.9 ms | 50.0 ms | 61.0 ms | 24.8 | 2.4 ms | 2.6 ms | 2.7 ms | 407.5 |
| key-100-c1 | 45.6 ms | 56.8 ms | 86.1 ms | 21.3 | 5.7 ms | 6.6 ms | 8.1 ms | 148.8 |
| list-1000-c1 | 231.1 ms | 263.2 ms | 277.4 ms | 4.5 | 28.9 ms | 42.9 ms | 46.8 ms | 32.0 |
| key-1-c10 | 38.4 ms | 43.9 ms | 48.7 ms | 25.5 | 2.6 ms | 3.1 ms | 3.2 ms | 351.3 |
| key-100-c10 | 45.0 ms | 52.5 ms | 55.1 ms | 21.8 | 5.6 ms | 7.7 ms | 824.0 ms* | 34.3 |
| list-1000-c10 | 196.8 ms | 222.9 ms | 232.4 ms | 5.3 | 29.2 ms | 42.6 ms | 124.4 ms | 29.8 |
| key-1-c50 | 38.3 ms | 42.0 ms | 45.1 ms | 25.8 | 2.3 ms | 2.5 ms | 2.6 ms | 373.0 |
| key-100-c50 | 45.2 ms | 49.8 ms | 53.6 ms | 22.0 | 6.0 ms | 8.5 ms | 844.7 ms* | 28.5 |
| list-1000-c50 | 210.9 ms | 299.7 ms | 300.7 ms | 4.5 | 29.5 ms | 43.6 ms | 101.3 ms | 29.3 |
| key-1-c100 | 38.1 ms | 42.1 ms | 50.6 ms | 26.0 | 2.6 ms | 3.0 ms | 3.2 ms | 368.0 |
| key-100-c100 | 43.3 ms | 46.2 ms | 80.3 ms | 23.0 | 5.9 ms | 8.7 ms | 840.2 ms* | 27.8 |
| list-1000-c100 | 291.0 ms | 346.7 ms | 361.6 ms | 3.5 | 29.0 ms | 43.6 ms | 115.3 ms | 30.0 |

\* p99 spikes at high concurrency on the 100-key lookup scenario, likely caused by occasional replica sync pauses under load.

#### Replication Lag / Consistency

| Scenario | Write latency | Immediate stale reads | Stale read rate | Measured lag |
|----------|---------------|----------------------|-----------------|--------------|
| Single insert -> read by key | 220 ms | 1 | 100% | 33 ms |
| Batch insert -> list query | 7,897 ms | 0 | 0% | 4 ms |

A single insert will almost always produce one stale read immediately after the write. Batch inserts are slower to write but the replica observes them within a few milliseconds by the time the read is issued.

#### Key Takeaways

- Replica reads are 10x to 15x faster than primary reads for a remote client
- List queries benefit the most from replica placement
- Replication is asynchronous, so immediate read-after-write consistency is not guaranteed
- Replicas are best for read-heavy workloads and geographically distant clients
- Writes should always go to the primary
