# Nona Config Admin

[![Container CI](https://github.com/Ryware/nona-config/actions/workflows/container-image-ci.yml/badge.svg)](https://github.com/Ryware/nona-config/actions/workflows/container-image-ci.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/Ryware/nona-config/badge)](https://scorecard.dev/viewer/?uri=github.com/Ryware/nona-config)

Admin panel for Nona Config management system.

## Setup

```bash
npm install
npm run dev
```

## Verify

Run from `nona-config/admin`:

```bash
npx tsc -b
npx vitest run
npx eslint src --max-warnings 0
```

`eslint` is currently clean apart from the known `boundaries` deprecation warning emitted by the plugin itself.

## Configuration

Development uses same-origin API URLs and proxies backend routes through Vite:

```env
VITE_API_BASE_URL=
VITE_PROXY_TARGET=http://localhost:5027
```

Leave `VITE_API_BASE_URL` empty when the admin UI is served by the same host as
the API. Set it only when the frontend and API are deployed on different
origins.

## Tech Stack

- SolidJS + TypeScript
- Vite
- Tailwind CSS
- TanStack Query

## Key Admin Flows

### Project sub-pages

- Project detail pages do not use a shared project title/description header anymore.
- Each sub-page owns its own section header and actions:
  - Parameters: search, bulk import, add parameter
  - Environments: add environment
  - API Keys: add API key
  - Releases / Shared Links: page-specific actions in the section header

