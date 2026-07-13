---
title: CLI
description: Install and use the nona command.
---

The `nona` CLI manages admin workflows from a terminal. Use it for:

- login sessions
- saved defaults
- projects
- config entries
- API keys
- users
- Firebase Remote Config migration

The CLI is especially useful because Nona is self-hosted and operator-friendly by design, so many teams use it for repeatable administration even if they also use the admin UI.

## Install

```bash
npm install -g nona-cli
```

Windows options:

```powershell
choco install nona-cli
```

Release archives are also published for Windows, Linux, and macOS.

## When to use the CLI

Use the CLI when you want repeatable admin operations, terminal-based workflows, automation or scripting, a clean migration path from Firebase Remote Config, or access to history, rollback, and share-link workflows without relying only on the UI.

## Authenticate

```bash
nona auth login --base-url https://nona.example.com
nona auth whoami
```

`auth login` opens a browser and stores a session token, which makes interactive use easier than pasting a bearer token into every command.

## Create a project

```bash
nona projects create --name mobile-app
nona projects list
```

Use the CLI when you want a repeatable way to create a project from a terminal. Environment creation is currently an admin-UI workflow, so the usual sequence is to create the project with the CLI, then open the project in admin and click `Add Environment`.

## Save defaults

```bash
nona config set base-url https://nona.example.com
nona config set project mobile-app
nona config show
```

After defaults are saved, commands can omit `--base-url` and `--project`, which is helpful when you are doing repeated work on the same Nona instance and project.

## Manage config entries

```bash
nona entries list --project mobile-app --environment production
nona entries get --project mobile-app --environment production --key Features:Checkout
nona entries set --project mobile-app --environment production --key Features:Checkout --value true --scope client --content-type boolean
nona entries history --project mobile-app --environment production --key Features:Checkout
nona entries rollback --project mobile-app --environment production --key Features:Checkout --version 2
nona entries delete --project mobile-app --environment production --key Features:Checkout
```

If you already saved a default project with `nona config set project mobile-app`, the same commands can omit `--project`.

```bash
nona entries list --environment production
nona entries get --environment production --key Features:Checkout
nona entries set --environment production --key Features:Checkout --value true --scope client --content-type boolean
nona entries history --environment production --key Features:Checkout
nona entries rollback --environment production --key Features:Checkout --version 2
nona entries delete --environment production --key Features:Checkout
```

These workflows cover the day-to-day runtime model: inspect values, create or update entries, review history, and roll back changes.

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
nona keys create --project mobile-app --name "Web app" --scope client --environment production
nona keys list --project mobile-app
nona keys delete --project mobile-app --id 42
```

`nona keys show --project mobile-app` also works. `show` is an alias for `list`.

Use client-scoped API keys for frontend/mobile apps and server-scoped keys for backend-only reads whenever possible.

## Invite a user

```bash
nona users create --name "Jane Doe" --user-email jane@example.com --role editor
```

The CLI returns the invitation result so you can hand the invite link or token to the teammate who needs access.

## Migrate from Firebase Remote Config

```bash
nona migrate firebase --config ./nona.migration.json --dry-run
nona migrate firebase --config ./nona.migration.json
```

Use `--dry-run` before applying a migration.

See [Firebase migration](/docs/cli/firebase-migration/) for configuration, environment mapping, and conflict behavior.

## Why the CLI matters for migration

Migration is usually an operator workflow, not a day-to-day end-user workflow. The CLI fits that well because it handles repeatable execution, dry runs, configuration files, credentials and environment variables, and output you can review before production cutover.

## Environment variables

The CLI reads these values when flags are omitted:

| Variable | Used for |
|---|---|
| `NONA_CLI_BASE_URL` | Nona base URL |
| `NONA_CLI_PROJECT_NAME` | project name |
| `NONA_CLI_BEARER_TOKEN` | admin bearer token |
| `NONA_CLI_EMAIL` | migration/login email |
| `NONA_CLI_PASSWORD` | migration/login password |

## Related docs

- [CLI reference](/docs/cli/reference/)
- [Migration](/docs/migration/)
- [Parameter share links](/docs/parameter-share-links/)
- [History and rollback](/docs/concepts/history-and-rollback/)

## FAQ

### When should I use the CLI instead of the admin UI?

Use the CLI for repeatable operations, scripting, migration work, history and rollback workflows, and terminal-first administration.

### Do I still need the admin UI if I use the CLI?

Often yes.

Some workflows such as environment creation are still documented primarily through the admin UI.

### What is the best first CLI command to run?

After installation, `nona auth login --base-url https://nona.example.com` is usually the best first command because it establishes the interactive session.

### Why is the CLI especially important for Firebase migration?

Because migration is an operator workflow that benefits from dry runs, config files, and repeatable execution from a terminal.
