---
title: Primary/replica production
description: Configure the production primary and replica Docker Compose deployment.
---

Use primary/replica mode for read-heavy deployments where eventual consistency is acceptable. This topology is for teams that need more than the simplest single-instance model and are willing to trade simplicity for a dedicated read path.

Compose file:

```text
deploy/compose/primary-replica-prod.yml
```

## Start

From the repository root:

```bash
docker compose -f deploy/compose/primary-replica-prod.yml up -d
```

## When to choose primary/replica

Choose this mode when:

- admin and write traffic should stay on the primary
- read-heavy workloads benefit from a separate replica
- eventual consistency is acceptable for replicated reads

Do not choose it just because it sounds more production-like. For many teams, standalone remains the better operational choice.

## Services and ports

| Service | API | libSQL HTTP | Replication gRPC |
|---|---:|---:|---:|
| `nona-primary` | `18081 -> 8080` | `19080 -> 9080` | `15001 -> 5001` |
| `nona-replica` | `18082 -> 8080` | `19082 -> 9080` | not exposed |

Use the primary API for admin and write workflows. Use the replica API for read-heavy clients when eventual consistency is acceptable.

Replication is asynchronous, so a value written to the primary may not be immediately visible from the replica. If your application requires an immediately visible write before the next read, keep that read path on the primary.

## Configure ports

Set these environment variables before `docker compose up`:

| Variable | Default | Meaning |
|---|---:|---|
| `NONA_PRIMARY_API_PORT` | `18081` | Host port for the primary Nona API |
| `NONA_PRIMARY_LIBSQL_PORT` | `19080` | Host port for the primary libSQL HTTP service |
| `NONA_PRIMARY_GRPC_PORT` | `15001` | Host port for primary replication gRPC |
| `NONA_REPLICA_API_PORT` | `18082` | Host port for the replica Nona API |
| `NONA_REPLICA_LIBSQL_PORT` | `19082` | Host port for the replica libSQL HTTP service |

Example:

```bash
NONA_PRIMARY_API_PORT=8081 \
NONA_REPLICA_API_PORT=8082 \
docker compose -f deploy/compose/primary-replica-prod.yml up -d
```

## Primary configuration

The primary exposes libSQL replication gRPC inside the Compose network:

```yaml
Storage__Libsql__ManagedPrimary__ExtraArgs__0: --grpc-listen-addr
Storage__Libsql__ManagedPrimary__ExtraArgs__1: 0.0.0.0:5001
Storage__Libsql__ManagedPrimary__ExtraArgs__2: --http-self-url
Storage__Libsql__ManagedPrimary__ExtraArgs__3: http://nona-primary:9080
```

The `--grpc-listen-addr` argument also makes `Storage:Type=Auto` select the libSQL primary provider at startup. No separate storage-provider environment variable is required.

The compose file also sets libSQL connection and request limits:

```yaml
Storage__Libsql__ManagedPrimary__ExtraArgs__4: --max-concurrent-connections
Storage__Libsql__ManagedPrimary__ExtraArgs__5: "4096"
Storage__Libsql__ManagedPrimary__ExtraArgs__6: --max-concurrent-requests
Storage__Libsql__ManagedPrimary__ExtraArgs__7: "4096"
```

## Replica configuration

The replica connects to the primary over the Compose service name `nona-primary`:

```yaml
Storage__Libsql__ManagedPrimary__ExtraArgs__0: --primary-grpc-url
Storage__Libsql__ManagedPrimary__ExtraArgs__1: http://nona-primary:5001
Storage__Libsql__ManagedPrimary__ExtraArgs__2: --http-self-url
Storage__Libsql__ManagedPrimary__ExtraArgs__3: http://nona-replica:9080
```

Keep `--primary-grpc-url` pointed at the primary service unless you also change the Compose service name or network.

The `--primary-grpc-url` argument makes `Storage:Type=Auto` select the libSQL replica provider at startup.

## Persistent data

The compose file creates two Docker volumes:

| Volume | Mounted path | Service |
|---|---|---|
| `nona-primary-data` | `/var/lib/nona` | `nona-primary` |
| `nona-replica-data` | `/var/lib/nona` | `nona-replica` |

Keep these volumes when upgrading containers and treat both as production data.

## JWT settings

If you pin JWT values, use the same values on both services:

```yaml
Jwt__Key: ${NONA_JWT_KEY:?set NONA_JWT_KEY}
Jwt__Issuer: ${NONA_JWT_ISSUER:-nona}
Jwt__Audience: ${NONA_JWT_AUDIENCE:-nona}
```

Set `NONA_JWT_KEY` from your production secret store or `.env` file.

## Operate

```bash
docker compose -f deploy/compose/primary-replica-prod.yml ps
docker compose -f deploy/compose/primary-replica-prod.yml logs -f nona-primary nona-replica
docker compose -f deploy/compose/primary-replica-prod.yml down
```

## FAQ

### Should I use primary/replica just because it sounds more production-like?

No.

For many teams, standalone is still the better production choice.

### What is the biggest tradeoff in primary/replica mode?

Eventual consistency on the replica read path.

Writes on the primary may not be visible on the replica immediately.

### Should admin and write traffic go to the replica?

No.

Admin and write workflows should stay on the primary.

### What should I validate after bringing up this topology?

Validate the primary admin and write path, the replica read path, the expected ports, and the replication relationship.

## Related docs

- [Deployment overview](/docs/deployment/)
- [Standalone production](/docs/deployment/standalone/)
