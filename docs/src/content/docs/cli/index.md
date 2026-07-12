---
title: CLI
description: Install and use the nona command.
---

The `nona` CLI manages admin workflows from a terminal.

Use it for:

- login sessions
- saved defaults
- projects
- config entries
- API keys
- users
- Firebase Remote Config migration

## Install

```bash
npm install -g nona-cli
```

Windows options:

```powershell
choco install nona-cli
```

Release archives are also published for Windows, Linux, and macOS.

## Authenticate

```bash
nona auth login --base-url https://nona.example.com
nona auth whoami
```

`auth login` opens a browser and stores a session token.

## Save defaults

```bash
nona config set base-url https://nona.example.com
nona config set project mobile-app
nona config show
```

After defaults are saved, commands can omit `--base-url` and `--project`.

## Manage config entries

```bash
nona entries list --environment production
nona entries get --environment production --key Features:Checkout
nona entries set --environment production --key Features:Checkout --value true --scope client --content-type boolean
nona entries history --environment production --key Features:Checkout
nona entries rollback --environment production --key Features:Checkout --version 2
nona entries delete --environment production --key Features:Checkout
```

## Share config entries

```bash
nona entries share create --environment production --key Features:Checkout --expiration 1h
nona entries share create --environment production --key Features:Checkout --view-only
nona entries share list --environment production --key Features:Checkout
nona entries share revoke --environment production --key Features:Checkout --id 11
```

See [Parameter share links](/docs/parameter-share-links/) for expiration options, permissions, and public endpoints.

## Manage API keys

```bash
nona keys create --name "Web app" --scope client --environment production
nona keys list
nona keys delete --id 42
```

Use client-scoped API keys for frontend/mobile apps.

## Migrate from Firebase Remote Config

```bash
nona migrate firebase --config ./nona.migration.json --dry-run
nona migrate firebase --config ./nona.migration.json
```

Use `--dry-run` before applying a migration.

See [Firebase migration](/docs/cli/firebase-migration/) for configuration, environment mapping, and conflict behavior.

## Environment variables

The CLI reads these values when flags are omitted:

| Variable | Used for |
|---|---|
| `NONA_CLI_BASE_URL` | Nona base URL |
| `NONA_CLI_PROJECT_NAME` | project name |
| `NONA_CLI_BEARER_TOKEN` | admin bearer token |
| `NONA_CLI_EMAIL` | migration/login email |
| `NONA_CLI_PASSWORD` | migration/login password |

## Command reference

See [CLI reference](/docs/cli/reference/) for generated command help.
