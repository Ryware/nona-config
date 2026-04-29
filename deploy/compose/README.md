# Compose Files

Run these from this folder unless noted:

```powershell
$env:NONA_JWT_KEY = "replace-with-a-long-random-secret"
docker compose -f standalone.yml up -d --build
```

`NONA_BUILD_CONTEXT` defaults to the repo parent folder. Set it only when the source tree is elsewhere, for example `/opt/nonaTest/src`.

## Files

`standalone.yml`
- One Nona instance with frontend, API, and managed libSQL.
- Use for local demos, smoke tests, and single-node installs.
- Ports: `NONA_API_PORT` default `18080`, `NONA_LIBSQL_PORT` default `19080`.

`primary-replica.local.yml`
- Primary and replica on one Docker network.
- Use to test replication on one machine.
- Ports: primary API `18081`, primary libSQL `19080`, primary gRPC `15001`, replica API `18082`, replica libSQL `19082`.

`remote-primary.yml`
- Primary node for a multi-host setup.
- Linux only: uses `network_mode: host`.
- Required: `NONA_PRIMARY_PUBLIC_HOST`, `NONA_JWT_KEY`.
- Exposes API `28081`, libSQL HTTP `29080`, gRPC `25001` by default.

`remote-replica.yml`
- Replica node for a multi-host setup.
- Linux only: uses `network_mode: host`.
- Required: `NONA_REPLICA_PUBLIC_HOST`, `NONA_PRIMARY_GRPC_URL`, `NONA_JWT_KEY`.
- Example: `NONA_PRIMARY_GRPC_URL=http://10.77.10.222:25001`.

## Common Settings

- `NONA_JWT_KEY`: required. Use a long random secret.
- `NONA_JWT_ISSUER`, `NONA_JWT_AUDIENCE`: optional, default `nona`.
- `NONA_DEFAULT_ENVIRONMENT`: optional, default `Production`.
- `NONA_BUILD_CONTEXT`: path containing both `NonaBackend` and `nona-config-admin`.

## Hints

- The main app image is `nona-combined:dev`; it includes the built frontend.
- Data is stored in named Docker volumes under `/var/lib/nona`.
- For remote replicas, ensure the replica host can reach the primary gRPC port.
- Keep `--http-self-url`, connection limits, and connection timeout in place; they prevent libSQL HTTP 429 errors under load.
