# Nona — Open Source Firebase Remote Config Alternative

**Self-hosted feature flags and remote configuration for web, mobile, and backend apps.**

[![Docker Pulls](https://img.shields.io/docker/pulls/rywaredev/nona?style=flat-square&logo=docker)](https://hub.docker.com/r/rywaredev/nona)
[![npm](https://img.shields.io/npm/v/nona-client?style=flat-square&logo=npm)](https://www.npmjs.com/package/nona-client)
[![NuGet](https://img.shields.io/nuget/v/Nona.Client?style=flat-square&logo=nuget)](https://www.nuget.org/packages/Nona.Client)
[![Chocolatey](https://img.shields.io/chocolatey/v/nona-cli?style=flat-square&logo=chocolatey)](https://community.chocolatey.org/packages/nona-cli)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-green.svg?style=flat-square)](LICENSE.txt)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/Ryware/nona-config/badge)](https://scorecard.dev/viewer/?uri=github.com/Ryware/nona-config)

Nona gives you the same feature flag and remote config capabilities as Firebase Remote Config — without the Google account, without the lock-in, and running entirely on your own infrastructure.

- Toggle **feature flags** from a dashboard without redeploying
- Update **mobile app config** (iOS, Android, React Native, Flutter) without an app store release
- Use **kill switches** to disable broken features in seconds
- Fetch everything via **one REST API call** — no SDK required in any language

> 🌐 [nonaconfig.com](https://nonaconfig.com) &nbsp;·&nbsp; 🐳 [Docker Hub](https://hub.docker.com/r/rywaredev/nona) &nbsp;·&nbsp; 📦 [npm](https://www.npmjs.com/package/nona-client) &nbsp;·&nbsp; 📦 [NuGet](https://www.nuget.org/packages/Nona.Client)

---

## Table of Contents

- [Why We Open Sourced Nona](#why-we-open-sourced-nona)
- [Why Nona](#why-nona)
- [Quick Start](#quick-start)
- [Repository Layout](#repository-layout)
- [Client Libraries](#client-libraries)
- [API](#api)
- [Docker Compose](#docker-compose)
- [Migrate from Firebase Remote Config](#migrate-from-firebase-remote-config)
- [Performance](#performance)
- [Architecture](#architecture)

---

## Why We Open Sourced Nona

Remote configuration is too often bundled with platform lock-in. We wanted teams to be able to change application behavior quickly without giving up control of their own infrastructure, data ownership, or platform choice.

Nona is our attempt to keep this part of the stack small and understandable: one Docker image, one HTTP API, official clients where they help, and a real migration path away from Firebase Remote Config. The longer story is here: [Why we open sourced Nona](https://nonaconfig.com/why-we-open-sourced-nona).

---

## Why Nona

| | Nona | Firebase Remote Config |
|---|---|---|
| Open source | ✅ Apache 2.0 licence | ❌ Closed source |
| Self-hostable | ✅ Docker / Kubernetes | ❌ Google-hosted only |
| No Google account | ✅ | ❌ Required |
| Works without mobile SDK | ✅ Plain HTTP | ❌ Firebase SDK needed |
| .NET / NuGet client | ✅ | ❌ |
| Migration tool | ✅ Built into CLI | — |
| Free forever | ✅ Self-host | Free tier with limits |

Nona runs as a single Docker container with SQLite in standalone mode and embedded [libSQL](https://github.com/tursodatabase/libsql) when primary/replica replication is configured — no external database, separate control plane, or cloud dependency.

---

## Quick Start

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

- **Web UI:** `http://localhost:18080`
- **API base:** `http://localhost:18080`
- **Guided setup:** `https://nonaconfig.com/docs/get-started/`

Create a project, add an environment, set your first key-value pair, publish a release, and set it active. Then fetch the value:

```bash
curl "http://localhost:18080/api/production/Features%3ACheckout" \
  -H "X-Api-Key: your-api-key"
```

```http
HTTP/1.1 200 OK
X-Nona-Content-Type: boolean

true
```

The API key is bound to one project, so the request path only needs the environment and key. For a full walkthrough, start with [First project](https://nonaconfig.com/docs/get-started/first-project/) and [First API call](https://nonaconfig.com/docs/get-started/first-api-call/).

If you are expecting LaunchDarkly-style evaluation, this is the key distinction: Nona reads are keyed by project, environment, scope, and key. There is no built-in runtime targeting, percentage rollout, or `userId`-based evaluation on the HTTP read path.

---

## Repository Layout

This repository is the Nona monorepo:

- `core`, `cli`, `libsql`, `migrator`: backend API, CLI, storage library, and migration tooling
- `admin`: admin web UI
- `client`: JavaScript SDK, .NET SDK, and JavaScript OpenFeature provider
- `docs`: documentation site

---

## Client Libraries

### JavaScript / Node.js / React Native

```bash
npm install nona-client
```

```js
import { createNonaClient } from "nona-client";

const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  environmentId: "production",
  apiKey: process.env.NONA_API_KEY,
  releaseVersion: "1.1.x"
});

const value = await nona.getConfigValue("Features:Checkout");
console.log(value.value);
```

📦 [npmjs.com/package/nona-client](https://www.npmjs.com/package/nona-client)

---

### .NET / C#

```bash
dotnet add package Nona.Client
```

```csharp
using Nona.Client;

var client = new NonaClient("https://nona.example.com", "production", apiKey: "your-api-key");
var value = await client.GetConfigValueAsync("Features:Checkout");
Console.WriteLine(value.Value);
```

📦 [nuget.org/packages/Nona.Client](https://www.nuget.org/packages/Nona.Client)

---

### OpenFeature / JavaScript

```bash
npm install nona-client nona-openfeature-provider @openfeature/server-sdk
```

See [client/javascript-openfeature-provider/README.md](client/javascript-openfeature-provider/README.md) for setup and usage.

---

### Any language (plain HTTP)

No SDK needed. A single GET request returns one config value from the environment's active release, or from a pinned release version:

```bash
# curl
curl "https://your-nona-host/api/production/Features%3ACheckout?version=1.1.x" \
  -H "X-Api-Key: your-api-key"

# Python
import httpx
value = httpx.get(
    "https://your-nona-host/api/production/Features%3ACheckout",
    headers={"X-Api-Key": api_key}
).text

# Go
req, _ := http.NewRequest("GET", "https://your-nona-host/api/production/Features%3ACheckout", nil)
req.Header.Set("X-Api-Key", apiKey)
```

The response body is the stored value. Nona also returns the logical type in the `X-Nona-Content-Type` response header.

---

### CLI (Windows / macOS / Linux)

```bash
# npm
npm install -g nona-cli
```

```powershell
# Windows via Chocolatey
choco install nona-cli
```

Or download the binary from [GitHub Releases](https://github.com/ryware/nona-config/releases).

CLI packages:

- [npmjs.com/package/nona-cli](https://www.npmjs.com/package/nona-cli)
- [community.chocolatey.org/packages/nona-cli](https://community.chocolatey.org/packages/nona-cli)

---

## API

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/{environmentId}/{key}` | Fetch one key from the active release |
| `GET` | `/api/{environmentId}/{key}?version=1.1.0` | Fetch one key from an exact release |
| `GET` | `/api/{environmentId}/{key}?version=1.1.x` | Fetch one key from the highest patch in a release line |
| `GET` | `/api/{environmentId}` | Fetch all client-visible keys with ETag support |

Authentication: `X-Api-Key` request header.

The API key determines the project. The response body contains the raw stored value, and `X-Nona-Content-Type` tells the client whether the value is `text`, `number`, `boolean`, or `json`.

The API does not accept per-user evaluation context for runtime flag resolution. Query parameters or headers such as `userId` or `X-User-Id` are not part of the Nona read model.

See [HTTP client docs](https://nonaconfig.com/docs/clients/http/) for examples and troubleshooting.

---

## Docker Compose

### Standalone 

Copy [`deploy/compose/standalone-prod.yml`](deploy/compose/standalone-prod.yml) to your server:

```bash
docker compose -f standalone-prod.yml up -d
```

Default host port: `http://localhost:18080`

| Variable | Default | Description |
|----------|---------|-------------|
| `NONA_API_PORT` | `18080` | Host port mapped to the API |
| `Jwt__Key` | auto-generated | JWT signing key |
| `Jwt__Issuer` | `nona` | JWT issuer claim |
| `Jwt__Audience` | `nona` | JWT audience claim |

### Primary / Replica

For read-heavy workloads or geographically distributed deployments, use [`deploy/compose/primary-replica-prod.yml`](deploy/compose/primary-replica-prod.yml):

```bash
docker compose -f primary-replica-prod.yml up -d
```

| Service | API port | libSQL port | gRPC port |
|---------|----------|-------------|-----------|
| `nona-primary` | `18081` | `19080` | `15001` |
| `nona-replica` | `18082` | `19082` | — |

The replica connects to the primary over gRPC and syncs automatically. Reads served from the replica are **10–15× faster** for remote clients (see [Performance](#performance)).

### JWT Settings

Nona auto-generates JWT settings on first start. To pin your own values:

```bash
docker run -d \
  --name nona \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  -e Jwt__Key=<your-secret-key> \
  -e Jwt__Issuer=nona \
  -e Jwt__Audience=nona \
  rywaredev/nona:latest
```

---

## Migrate from Firebase Remote Config

The Nona CLI includes a built-in Firebase Remote Config migration command that imports your existing parameters using a migration config file.

```bash
# Install CLI
choco install nona-cli

# Run migration
nona migrate firebase \
  --config ./nona.migration.json \
  --base-url http://localhost:18080
```

See [`cli/src/Nona.Cli/README.md`](cli/src/Nona.Cli/README.md) for the full CLI reference.

---

## Performance

All measurements from production-equivalent environments.

### Single-node (SQLite local)

Dataset: **10,000 rows**, p95 latency:

| Scenario | p95 (ms) | req/s |
|----------|----------|-------|
| Point lookup — 1 key | 0.154 | 11,248 |
| Point lookup — 100 keys | 0.712 | 1,616 |
| Range query — 1,000 rows | 3.703 | 289 |

All targets pass with 0% error rate under load.

### Primary / Replica (remote client)

| Scenario | Primary p95 | Replica p95 |
|----------|-------------|-------------|
| 1 key, c1 | 50.0 ms | **2.6 ms** |
| 100 keys, c1 | 56.8 ms | **6.6 ms** |
| 1,000 rows, c1 | 263.2 ms | **42.9 ms** |

Replica reads are **10–15× faster** for geographically distant clients.

> **Note:** Replication is asynchronous. Immediate read-after-write consistency is not guaranteed — replicas are best for read-heavy workloads where eventual consistency is acceptable.

---

## Architecture

```
┌─────────────────────────────────────┐
│  Nona Container (rywaredev/nona)    │
│                                     │
│  ┌──────────┐   ┌─────────────────┐ │
│  │ Web UI   │   │   HTTP API      │ │
│  │ :8080    │   │   :8080         │ │
│  └──────────┘   └─────────┬───────┘ │
│                           │         │
│                  ┌────────▼────────┐│
│                  │ SQLite / libSQL ││
│                  │  /var/lib/nona  ││
│                  └─────────────────┘│
└─────────────────────────────────────┘
```

- **No external database** — standalone uses SQLite; sqld is bundled for primary/replica mode
- **Single port** — API and Web UI share port 8080
- **Persistent volume** — mount `/var/lib/nona` to survive container restarts
- **Optional replica** — add a read replica with the primary/replica compose file

---

## Contributing

Issues and pull requests are welcome. See the [issues tracker](https://github.com/ryware/nona-config/issues) to report bugs or request features.

---

## Licence

[Apache 2.0](LICENSE.txt) — free to use, self-host, and modify.

Built by [Ryware.dev](https://ryware.dev)
