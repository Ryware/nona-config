# Nona CLI

`Nona.Cli` packages the Nona admin and migration workflows as a `dotnet tool`.

## Install

Build and pack it locally:

```bash
dotnet pack cli/src/Nona.Cli/Nona.Cli.csproj -c Release
```

Install from the generated package:

```bash
dotnet tool install --global --add-source ./cli/src/Nona.Cli/bin/Release Nona.Cli
```

The installed command name is `nona`.

## Commands

Show project API keys:

```bash
nona keys show --project mobile-app --base-url https://nona.example.com --token <token>
```

Reroll one or both keys:

```bash
nona keys reroll --project mobile-app --type both --base-url https://nona.example.com --token <token>
```

Run a Firebase Remote Config migration:

```bash
nona migrate firebase --config ./nona.migration.json --base-url https://nona.example.com --email admin@example.com --password secret
```

## On-Prem

For on-prem deployments, point the CLI at the local API host:

```bash
nona keys show --project mobile-app --base-url http://nona.internal:18080 --token <token>
```

You can also use environment variables:

- `NONA_CLI_BASE_URL`
- `NONA_CLI_PROJECT_NAME`
- `NONA_CLI_BEARER_TOKEN`
- `NONA_CLI_EMAIL`
- `NONA_CLI_PASSWORD`

The Firebase migration command also continues to support the existing `NONA_MIGRATOR_*` environment variables.
