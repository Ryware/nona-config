# Nona — Open Source Firebase Remote Config Alternative

**Self-hosted feature flags and remote configuration for web, mobile, and backend apps.**

[![Docker Pulls](https://img.shields.io/docker/pulls/rywaredev/nona?style=flat-square&logo=docker)](https://hub.docker.com/r/rywaredev/nona)
[![npm](https://img.shields.io/npm/v/nona-client?style=flat-square&logo=npm)](https://www.npmjs.com/package/nona-client)
[![NuGet](https://img.shields.io/nuget/v/Nona.Client?style=flat-square&logo=nuget)](https://www.nuget.org/packages/Nona.Client)
[![Chocolatey](https://img.shields.io/chocolatey/v/nona-cli?style=flat-square&logo=chocolatey)](https://community.chocolatey.org/packages/nona-cli)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-green.svg?style=flat-square)](LICENSE.txt)

Nona gives you the same feature flag and remote config capabilities as Firebase Remote Config — without the Google account, without the lock-in, and running entirely on your own infrastructure.

- Toggle **feature flags** from a dashboard without redeploying
- Update **mobile app config** (iOS, Android, React Native, Flutter) without an app store release
- Use **kill switches** to disable broken features in seconds
- Fetch everything via **one REST API call** — no SDK required in any language

> 🌐 [nonaconfig.com](https://nonaconfig.com) &nbsp;·&nbsp; 🐳 [Docker Hub](https://hub.docker.com/r/rywaredev/nona) &nbsp;·&nbsp; 📦 [npm](https://www.npmjs.com/package/nona-client) &nbsp;·&nbsp; 📦 [NuGet](https://www.nuget.org/packages/Nona.Client)

---

## Table of Contents

- [Why Nona](#why-nona)
- [Quick Start](#quick-start)
- [Client Libraries](#client-libraries)
- [API](#api)
- [Docker Compose](#docker-compose)
- [Migrate from Firebase Remote Config](#migrate-from-firebase-remote-config)
- [Performance](#performance)
- [Architecture](#architecture)

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

Nona runs as a single Docker container with an embedded [libSQL](https://github.com/tursodatabase/libsql) database — no external database, no separate control plane, no cloud dependency.

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

Create a project, add environments, and set your first key-value pair. Then fetch your config:

```bash
curl http://localhost:18080/v1/config/my-app/production \
  -H "X-API-Key: your-api-key"
```

```json
{
  "checkout_v2": true,
  "dark_mode": false,
  "banner_text": "Hello",
  "max_upload_mb": 50
}
```

---

## Client Libraries

### JavaScript / Node.js / React Native

```bash
npm install nona-client
```

```js
import { createClient } from 'nona-client'

const nona = createClient({ apiKey: process.env.NONA_API_KEY })
const config = await nona.get('my-app', 'production')

if (config.checkout_v2) {
  // show new checkout UI
}
```

📦 [npmjs.com/package/nona-client](https://www.npmjs.com/package/nona-client)

---

### .NET / C#

```bash
dotnet add package Nona.Client
```

```csharp
var client = new NonaClient(apiKey);
var config = await client.GetAsync("my-app", "production");

if (config.GetBool("checkout_v2"))
{
    // show new checkout UI
}
```

📦 [nuget.org/packages/Nona.Client](https://www.nuget.org/packages/Nona.Client)

---

### Any language (plain HTTP)

No SDK needed. A single GET request returns all config for a project and environment as JSON:

```bash
# curl
curl https://your-nona-host/v1/config/{project}/{environment} \
  -H "X-API-Key: your-api-key"

# Python
import httpx
config = httpx.get(
    "https://your-nona-host/v1/config/my-app/production",
    headers={"X-API-Key": api_key}
).json()

# Go
req, _ := http.NewRequest("GET", "https://your-nona-host/v1/config/my-app/production", nil)
req.Header.Set("X-API-Key", apiKey)
```

---

### CLI (Windows / macOS / Linux)

```powershell
# Windows via Chocolatey
choco install nona-cli
```

Or download the binary from [GitHub Releases](https://github.com/ryware/nona-config/releases).

🍫 [community.chocolatey.org/packages/nona-cli](https://community.chocolatey.org/packages/nona-cli)

---

## API

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/config/{project}/{environment}` | Fetch all config for a project/environment |
| `GET` | `/v1/config/{project}/{environment}/{key}` | Fetch a single key |

Authentication: `X-API-Key` request header.

---

## Docker Compose

### Standalone (recommended for most deployments)

Copy [`deploy/compose/standalone-prod.yml`](deploy/compose/standalone-prod.yml) to your server:

```bash
docker compose up -d
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
docker compose up -d
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

The Nona CLI includes a built-in Firebase Remote Config migration command that imports all your existing parameters into a Nona project.

```powershell
# Install CLI
choco install nona-cli

# Run migration
nona migrate firebase \
  --firebase-project your-project-id \
  --firebase-credentials path/to/service-account.json \
  --nona-host http://localhost:18080 \
  --nona-project my-app \
  --nona-environment production
```

See [`cli/src/Nona.Cli/README.md`](cli/src/Nona.Cli/README.md) for the full CLI reference.

---

## Performance

All measurements from production-equivalent environments.

### Single-node (libSQL local)

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
│                  │  embedded libSQL││
│                  │  /var/lib/nona  ││
│                  └─────────────────┘│
└─────────────────────────────────────┘
```

- **No external database** — libSQL is bundled in the container image
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
