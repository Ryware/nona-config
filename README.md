# Nona Config

Production container image: `rywaredev/nona:latest`

Nona is a self-hosted remote configuration service backed by [libSQL](https://github.com/tursodatabase/libsql). The container bundles both the .NET API and the `sqld` libSQL server — no external database is required.

> Created by [Ryware.dev](https://ryware.dev) · Full documentation at [nonaconfig.com](https://www.nonaconfig.com/)

The standalone container exposes:

| Port | Purpose |
|------|---------|
| `8080` | HTTP API + embedded web UI |
| `9080` | libSQL HTTP endpoint (primary/replica communication) |

In the primary–replica setup the primary additionally exposes port `5001` (gRPC) for replica synchronisation.

Persistent data is stored in `/var/lib/nona`. Mount this path as a Docker volume so the database survives container restarts.

---

## Quick Start — Docker Run

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

The web UI is available at `http://localhost:18080` once the container is running.

### JWT Settings

Nona generates and persists JWT settings automatically on first start when `Jwt__Key`, `Jwt__Issuer`, and `Jwt__Audience` are **not** provided. To pin your own values, pass all three:

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

---

## Docker Compose — Standalone

Copy [`deploy/compose/standalone-prod.yml`](deploy/compose/standalone-prod.yml) to your server and rename it to `docker-compose.yml`.

```bash
docker compose up -d
```

Default host port: `http://localhost:18080`

Optional environment variables (uncomment in the compose file to override):

| Variable | Default | Description |
|----------|---------|-------------|
| `NONA_API_PORT` | `18080` | Host port mapped to the API |
| `Jwt__Key` | auto-generated | JWT signing key |
| `Jwt__Issuer` | `nona` | JWT issuer claim |
| `Jwt__Audience` | `nona` | JWT audience claim |

---

## Docker Compose — Primary / Replica

A primary–replica setup runs two Nona containers sharing the same libSQL dataset. Reads can be served locally from the replica while writes go to the primary, which is the main driver of the read-replica performance advantage (see [Performance](#performance) below).

Copy [`deploy/compose/primary-replica-prod.yml`](deploy/compose/primary-replica-prod.yml) to your server and rename it to `docker-compose.yml`.

```bash
docker compose up -d
```

Default host ports:

| Service | API port | libSQL port | gRPC port |
|---------|----------|-------------|-----------|
| `nona-primary` | `18081` | `19080` | `15001` |
| `nona-replica` | `18082` | `19082` | — |

Override the ports via environment variables:

| Variable | Default |
|----------|---------|
| `NONA_PRIMARY_API_PORT` | `18081` |
| `NONA_PRIMARY_LIBSQL_PORT` | `19080` |
| `NONA_PRIMARY_GRPC_PORT` | `15001` |
| `NONA_REPLICA_API_PORT` | `18082` |
| `NONA_REPLICA_LIBSQL_PORT` | `19082` |

The replica connects to the primary over gRPC and syncs automatically. No additional configuration is required beyond the compose file.

---

## Performance

All measurements are from production-equivalent environments.

### SQLite vs libSQL (local)

Dataset: **10,000 rows** (medium). Results measured at p95 latency.

| Scenario | SQLite p95 (ms) | SQLite rps | libSQL p95 (ms) | libSQL rps |
|----------|-----------------|-----------|-----------------|-----------|
| Point lookup — 1 key, c1 | 0.088 | 18,226 | 0.154 | 11,248 |
| Point lookup — 1 key, c50 | 0.093 | 17,929 | 0.142 | 11,943 |
| Point lookup — 10 keys, c10 | 0.132 | 13,703 | 0.209 | 7,287 |
| Point lookup — 100 keys, c10 | 0.276 | 4,625 | 0.712 | 1,616 |
| Range query — 1,000 rows, c10 | 0.562 | 2,407 | 3.703 | 289 |
| Range query — 10,000 rows, c5 | 2.776 | 386 | 70.107 | 24 |

**Target compliance (libSQL local):**

| Target | Threshold | Result |
|--------|-----------|--------|
| Single key lookup p95 | ≤ 80 ms | ✅ 0.154 ms |
| 100-row query p95 | ≤ 120 ms | ✅ 0.756 ms |
| 1,000-row query p95 | ≤ 750 ms | ✅ 3.703 ms |
| Error rate under load | < 2% | ✅ 0.000% |
| No degradation at 50 users | — | ✅ 0.142 ms |

**Summary:** libSQL local passes all required targets with zero errors. SQLite is faster in absolute terms — the gap is minor for small point lookups but material for large range queries (libSQL is ~6.5× slower on average for required scenarios, with the most pronounced difference at 10,000-row range queries: 2.8 ms vs 70 ms p95).

---

### Read Replica Performance

All values measured from a secondary server (remote client) against a geographically separated primary and a co-located replica.

#### Read Performance (p50 / p95 / p99 / throughput)

| Scenario | Primary p50 | Primary p95 | Primary p99 | Primary rps | Replica p50 | Replica p95 | Replica p99 | Replica rps |
|----------|------------|------------|------------|------------|------------|------------|------------|------------|
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

\* p99 spikes at high concurrency on the 100-key lookup scenario — likely caused by occasional replica sync pauses under load.

#### Replication Lag / Consistency

| Scenario | Write latency | Immediate stale reads | Stale read rate | Measured lag |
|----------|--------------|----------------------|-----------------|-------------|
| Single insert → read by key | 220 ms | 1 | 100% | 33 ms |
| Batch insert → list query | 7,897 ms | 0 | 0% | 4 ms |

A single insert will almost always produce one stale read immediately after the write (replication lag ~33 ms). Batch inserts are slower to write but the replica observes them within ~4 ms by the time the read is issued. Plan reads accordingly or add a short read-after-write delay for latency-sensitive paths.

#### Key Takeaways

- **Replica reads are 10–15× faster** than primary reads for a remote client (cross-region or cross-datacenter). For a 1-key lookup the replica median drops from ~38 ms to ~2.5 ms.
- **List queries benefit most**: a 1,000-row list goes from ~230 ms (primary) to ~29 ms (replica) at the median.
- **Replication is asynchronous**: a single insert causes a stale read ~100% of the time immediately after the write; the lag clears within ~33 ms under normal conditions.
- **Use replicas** for read-heavy workloads, config fetches, and clients that are geographically far from the primary.
- **Route writes to the primary** always; replicas are read-only.
