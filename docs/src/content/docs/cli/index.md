---
title: CLI
description: Install and use the nona command.
---

The `nona` CLI manages admin workflows from a terminal. Use it for:

- login sessions
- saved defaults
- projects
- environments
- config entries
- releases
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

For a brand-new Nona instance, use `init` when you want the shortest path to a readable flag:

```bash
nona init \
  --yes \
  --base-url https://nona.example.com \
  --email admin@example.com \
  --password secret \
  --project mobile-app \
  --print-key
```

`init` registers the first admin when needed, logs in on existing instances, creates or reuses the project and environment, seeds a starter flag, creates or reuses a scoped API key, and prints app-ready environment variables.

```bash
nona auth register --base-url https://nona.example.com --email admin@example.com --password secret
nona auth login --base-url https://nona.example.com
nona auth whoami
```

`auth register` is the lower-level non-interactive first-admin command. It creates the initial admin account when the Nona instance has no users yet and saves the returned session token, so automation can continue with project, API key, and config commands without opening the admin UI.

`auth login` opens a browser and stores a session token, which makes interactive use easier than pasting a bearer token into every command.

## Bootstrap a first flag

The default `init` output is directly appendable to an app `.env` file:

```dotenv
# Nona - project "mobile-app", env "production"
VITE_NONA_BASE_URL=https://nona.example.com
VITE_NONA_ENV_ID=production
VITE_NONA_API_KEY=****158D
# API key masked; re-run with --print-key to emit a working value.
# Verify: curl -H "X-Api-Key: $VITE_NONA_API_KEY" https://nona.example.com/api/production/Features%3AExample
```

Useful options:

- `--yes` makes the command non-interactive: it never prompts and fails fast if a required value is missing.
- `--env production` chooses the environment to create or reuse.
- `--seed-flag Features:Example=true` changes the starter flag.
- `--no-seed-flag` skips starter flag creation.
- `--scope client|server|all` controls the API key and starter flag scope.
- `--format dotenv|json|env-export` changes the output format.
- `--password -` reads the password from stdin.
- `--print-key` prints the full API key instead of masking it.

## Create a project

```bash
nona projects create --name mobile-app
nona projects list
```

Use `nona init` for the first project and environment in a fresh setup. For later manual administration, create the project and manage its environments explicitly from the CLI.

## Manage environments

```bash
nona environments list --project mobile-app
nona environments create --project mobile-app --name development
nona environments delete --project mobile-app --name development
```

Creating an environment is idempotent: requesting an existing environment succeeds and reuses it. Config entry commands do not create missing environments implicitly.

## Manage releases

```bash
nona releases list --project mobile-app --environment production
nona releases view --project mobile-app --environment production --version 1.1.0
nona releases create --project mobile-app --environment production --version 1.2.0
nona releases amend --project mobile-app --environment production --source-version 1.1.0 --version 1.1.1
nona releases activate --project mobile-app --environment production --version 1.2.0
nona releases clear-active --project mobile-app --environment production
nona releases delete --project mobile-app --environment production --version 1.1.0
```

`nona releases create` snapshots the environment's current working configuration. Pass `--activate` if the newly created release should become active immediately.

`nona releases amend` copies the source release's entries unchanged into the new patch version. It does not edit the environment's working configuration. Use `--source-version` for the release to copy and `--version` for the new release.

An active release cannot be deleted. Activate another release or run `nona releases clear-active` first.

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
| `NONA_INIT_EMAIL` | `nona init` admin email |
| `NONA_INIT_PASSWORD` | `nona init` admin password |

## Related docs

- [CLI reference](/docs/cli/reference/)
- [Migration](/docs/migration/)
- [Parameter share links](/docs/parameter-share-links/)
- [History and rollback](/docs/concepts/history-and-rollback/)

## FAQ

### When should I use the CLI instead of the admin UI?

Use the CLI for repeatable operations, scripting, migration work, history and rollback workflows, and terminal-first administration.

### Do I still need the admin UI if I use the CLI?

Often yes. The CLI supports repeatable administration of projects, environments, config entries, API keys, and users. Use the admin UI when a visual workflow is more convenient.

### What is the best first CLI command to run?

For a fresh self-hosted instance, `nona init --yes --base-url https://nona.example.com --email admin@example.com --password secret --project mobile-app` is the best first command because it reaches a real flag read path without browser interaction.

For an existing instance where you want an interactive admin session, use `nona auth login --base-url https://nona.example.com`.

### Why is the CLI especially important for Firebase migration?

Because migration is an operator workflow that benefits from dry runs, config files, and repeatable execution from a terminal.
