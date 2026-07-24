---
title: Firebase migration
description: Migrate Firebase Remote Config parameters into Nona with the CLI, mapping environments and scopes and using dry runs before cutover.
---

Use `nona migrate firebase` to import Firebase Remote Config parameters into a Nona project.

The migrator reads Firebase Remote Config, builds a migration plan, creates the Nona project and environments when they do not exist, and then writes config entries.

## Requirements

- A running Nona backend.
- The `nona` CLI installed.
- A Firebase service account JSON file, or the same JSON value in an environment variable.
- Nona admin access with either a bearer token, email/password, or a saved `nona auth login` session.

If you use a saved login session, the saved session base URL must match the migration `baseUrl`.

## Create a config file

Create `nona.migration.json`:

```json
{
  "firebase": {
    "projectId": "your-firebase-project-id",
    "serviceAccountJsonPath": "./firebase-service-account.json",
    "sources": [
      {
        "namespace": "firebase",
        "scope": "client"
      },
      {
        "namespace": "firebase-server",
        "scope": "server"
      }
    ]
  },
  "nona": {
    "baseUrl": "https://nona.example.com",
    "projectName": "mobile-app"
  },
  "migration": {
    "dryRun": false,
    "renameConflictingKeys": true,
    "applyDefaultToMappedEnvironments": true,
    "defaultValueEnvironments": [
      "development"
    ],
    "conditionEnvironmentMappings": {
      "production": "production",
      "staging": "staging"
    }
  }
}
```

Keep secrets out of the file. Pass the Nona token and Firebase service account through environment variables when possible.

```bash
export NONA_MIGRATOR_NONA_BEARER_TOKEN="<admin-token>"
```

## Run a dry run

```bash
nona migrate firebase --config ./nona.migration.json --dry-run
```

The dry run prints planned writes and does not update Nona.

## Apply the migration

```bash
nona migrate firebase --config ./nona.migration.json
```

Keep `"dryRun": false` in the config file for the apply step. The `--dry-run` flag can turn a run into a preview, but there is no command-line flag that turns a config-file dry run back off.

## Firebase sources and scopes

If `firebase.sources` is set, each source is imported with its configured Nona scope:

| Source field | Meaning |
|---|---|
| `namespace` | Firebase Remote Config namespace. Omit or set empty to use the default Firebase API template. |
| `scope` | Nona scope for entries from this source: `client`, `server`, or `all`. |

If `sources` is omitted and `namespace` is set, the migrator imports that namespace with scope `all`. If both `sources` and `namespace` are omitted, the migrator imports two namespaces:

| Firebase namespace | Nona scope |
|---|---|
| `firebase` | `client` |
| `firebase-server` | `server` |

## Environment mapping

`defaultValueEnvironments` tells the migrator where Firebase default values should be written. `conditionEnvironmentMappings` maps Firebase condition names to Nona environment names. For each key and target environment, the migrator uses the first matching Firebase condition in Firebase condition order. If no condition matches and defaults apply to that environment, it uses the Firebase default value. When `applyDefaultToMappedEnvironments` is `true`, default values are also written to mapped environments that do not have a matching conditional value. Unmapped Firebase conditions are skipped with a warning.

## Content types

Firebase `valueType` is mapped to Nona content type:

| Firebase value type | Nona content type |
|---|---|
| `STRING` | `text` |
| `BOOLEAN` | `boolean` |
| `NUMBER` | `number` |
| `JSON` | `json` |
| `PARAMETER_VALUE_TYPE_UNSPECIFIED` | `text` |

Unknown Firebase value types are imported as `text` with a warning.

## Parameter groups

Parameters inside Firebase parameter groups are flattened and imported with the original parameter key. If the same key exists more than once while flattening groups, the migration stops with an error.

## Conflicts between sources

When multiple sources produce the same key in the same Nona environment:

- if values match, scopes are merged, for example `client` plus `server` becomes `all`
- if values differ and `renameConflictingKeys` is `false`, the first value is kept and the later value is skipped with a warning
- if values differ and `renameConflictingKeys` is `true`, the later key is renamed with a numeric suffix such as `_1`
- if values match but content types differ, the first content type is kept with a warning

## Config resolution

The migrator loads configuration in this order:

1. `NONA_MIGRATOR_CONFIG_PATH`
2. `--config <path>`
3. `./nona.migration.json`
4. `./appsettings.json`
5. the bundled sample `appsettings.json`

For values inside the config, environment variables override the file. Command-line flags override both.

| Variable | Meaning |
|---|---|
| `NONA_MIGRATOR_FIREBASE_PROJECT_ID` | Firebase project ID |
| `NONA_MIGRATOR_FIREBASE_SERVICE_ACCOUNT_PATH` | path to Firebase service account JSON |
| `NONA_MIGRATOR_FIREBASE_SERVICE_ACCOUNT_JSON` | Firebase service account JSON value |
| `NONA_MIGRATOR_NONA_BASE_URL` | Nona API base URL |
| `NONA_MIGRATOR_NONA_PROJECT_NAME` | Nona project name |
| `NONA_MIGRATOR_NONA_BEARER_TOKEN` | Nona admin bearer token |
| `NONA_MIGRATOR_NONA_EMAIL` | Nona admin email |
| `NONA_MIGRATOR_NONA_PASSWORD` | Nona admin password |
| `NONA_MIGRATOR_DRY_RUN` | `true` or `false` |
| `NONA_MIGRATOR_RENAME_CONFLICTING_KEYS` | `true` or `false` |
| `NONA_MIGRATOR_APPLY_DEFAULT_TO_MAPPED_ENVIRONMENTS` | `true` or `false` |
| `NONA_MIGRATOR_DEFAULT_ENVIRONMENTS` | comma-separated Nona environment names |
| `NONA_MIGRATOR_CONDITION_ENVIRONMENT_MAP_JSON` | JSON object mapping Firebase condition names to Nona environment names |

The migrator also reads these CLI environment variables for Nona connection values:

| Variable | Used as |
|---|---|
| `NONA_CLI_BASE_URL` | Nona API base URL |
| `NONA_CLI_PROJECT_NAME` | Nona project name |
| `NONA_CLI_BEARER_TOKEN` | Nona admin bearer token |
| `NONA_CLI_EMAIL` | Nona admin email |
| `NONA_CLI_PASSWORD` | Nona admin password |

## Command-line overrides

```bash
nona migrate firebase \
  --config ./nona.migration.json \
  --base-url https://nona.example.com \
  --project mobile-app \
  --token "$NONA_ADMIN_TOKEN" \
  --dry-run
```

Accepted aliases:

| Option | Alias |
|---|---|
| `--base-url <base-url>` | `--api-url <base-url>` |
| `--project <project-name>` | `--project-name <project-name>` |
| `--token <bearer-token>` | `--bearer-token <bearer-token>` |

`--email` and `--password` can be used instead of `--token`.

## FAQ

### Should I run a dry run first?

Yes.

The dry run is the safest first step because it shows how the migration will land before writing anything to Nona.

### Do Firebase booleans stay useful after migration?

Yes.

They map naturally into Nona `boolean` entries and continue to work well as feature flags.

### What happens to Firebase conditions?

They are mapped into Nona environments during migration instead of staying as Firebase-style runtime targeting rules.

### What is the biggest risk in this migration flow?

Assuming a technically successful import means the migration is finished.

You still need to validate environments, scopes, content types, and real application reads afterward.
