---
title: CLI reference
description: Generated command reference for the nona CLI.
---

Generated from the `nona` command help output.

Regenerate this page with:

```bash
npm run generate:cli
```

## Common options

The command help below omits repeated common options from individual option lists.

- `-?, -h, --help` is accepted by every command and subcommand.
- `--verbose` includes the full exception and stack trace when a command fails. Without it, stack traces are suppressed.
- Commands that connect to the Nona API may also accept `--api-url, --base-url <base-url>` and `--bearer-token, --token <bearer-token>`.
- Connection values can come from flags, `NONA_CLI_*` environment variables, saved defaults, or a matching `nona auth login` session.

## HTTP/API error output and exit codes

HTTP/API failures are written to standard error as one human-readable line, including the HTTP status and the server's error code when available:

```text
Error: value is not a valid number (400, INVALID_VALUE)
```

| Exit code | HTTP/API failure |
| --- | --- |
| `2` | Validation or other client request error (`400`, `422`, or another `4xx`) |
| `3` | Authentication or authorization error (`401` or `403`) |
| `4` | Resource not found (`404`) |
| `5` | Conflict (`409`) |
| `6` | Server error (`5xx`) |

Other command-specific failures may use different non-zero exit codes.

## `nona`

Administer Nona configuration through a command-line interface.

**Usage**

```text
nona [command] [options]
```

**Commands**

- `users` Invite users to Nona.
- `projects` List, create, and delete projects.
- `migrate` Run migration commands.
- `keys` List, create, and delete project API keys.
- `init` Bootstrap a Nona instance from first container start to first flag read. Exit codes: 0 success; 1 unexpected/API error; 2 invalid args; 3 auth failed; 4 cannot reach base-url.
- `entries` Read, write, and share config entries.
- `config` Show or save default CLI values.
- `auth` Sign in and manage saved sessions.

**Options**

```text
--version       Show version information
```

## `nona users`

Invite users to Nona.

**Usage**

```text
nona users [command] [options]
```

**Commands**

- `create` Invite a user and print the invitation token.

## `nona users create`

Invite a user and print the invitation token.

**Usage**

```text
nona users create [options]
```

**Options**

```text
--api-url, --base-url <base-url>        Nona base URL.
--bearer-token, --token <bearer-token>  Admin bearer token.
--name <name> (REQUIRED)                Full name of the new user.
--user-email <user-email> (REQUIRED)    Email address of the new user.
--role <role>                           User role: viewer or editor.
--scope <scope>                         User scope: client, server, or all.
```

## `nona projects`

List, create, and delete projects.

**Usage**

```text
nona projects [command] [options]
```

**Commands**

- `list` List projects.
- `create` Create a project.
- `delete` Delete a project.

## `nona projects list`

List projects.

**Usage**

```text
nona projects list [options]
```

**Options**

```text
--api-url, --base-url <base-url>        Nona base URL.
--bearer-token, --token <bearer-token>  Admin bearer token.
```

## `nona projects create`

Create a project.

**Usage**

```text
nona projects create [options]
```

**Options**

```text
--api-url, --base-url <base-url>        Nona base URL.
--bearer-token, --token <bearer-token>  Admin bearer token.
--name <name>                           Project name. Letters, numbers, and hyphens only.
```

## `nona projects delete`

Delete a project.

**Usage**

```text
nona projects delete [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
```

## `nona migrate`

Run migration commands.

**Usage**

```text
nona migrate [command] [options]
```

**Commands**

- `firebase` Import Firebase Remote Config into Nona.

## `nona migrate firebase`

Import Firebase Remote Config into Nona.

**Usage**

```text
nona migrate firebase [options]
```

**Options**

```text
--config <config>                         Migration config file path.
--dry-run                                 Preview changes without applying them.
--api-url, --base-url <base-url>          Nona base URL.
--project, --project-name <project-name>  Nona project name.
--bearer-token, --token <bearer-token>    Admin bearer token.
--email <email>                           Admin email used by the migrator when no token is supplied.
--password <password>                     Admin password used by the migrator when no token is supplied.
```

## `nona keys`

List, create, and delete project API keys.

**Usage**

```text
nona keys [command] [options]
```

**Commands**

- `list, show` List API keys for a project.
- `create` Create an API key for a project.
- `delete` Delete an API key.

## `nona keys list`

List API keys for a project.

**Usage**

```text
nona keys list [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--project, --project-name <project-name>  Nona project name.
--bearer-token, --token <bearer-token>    Admin bearer token.
```

## `nona keys create`

Create an API key for a project.

**Usage**

```text
nona keys create [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--project, --project-name <project-name>  Nona project name.
--name <name>                             API key name.
--env, --environment <environment>        Limit the key to one environment.
--scope <scope>                           Read scope for the key: client, server, or all.
--bearer-token, --token <bearer-token>    Admin bearer token.
```

## `nona keys delete`

Delete an API key.

**Usage**

```text
nona keys delete [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--project, --project-name <project-name>  Nona project name.
--id <id>                                 API key id to delete.
--bearer-token, --token <bearer-token>    Admin bearer token.
```

## `nona init`

Bootstrap a Nona instance from first container start to first flag read. Exit codes: 0 success; 1 unexpected/API error; 2 invalid args; 3 auth failed; 4 cannot reach base-url.

**Usage**

```text
nona init [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL. Env: NONA_CLI_BASE_URL. Default: http://localhost:18080.
--email <email>                           Admin email. Env: NONA_INIT_EMAIL.
--password <password>                     Admin password. Env: NONA_INIT_PASSWORD. Use '-' to read one line from stdin.
--project, --project-name <project-name>  Project name. Env: NONA_CLI_PROJECT_NAME. Letters, numbers, and hyphens only.
--env <env>                               Environment to create or reuse. Default: production.
--seed-flag <seed-flag>                   Starter flag as key=value. Default: Features:Example=true.
--no-seed-flag                            Skip starter flag creation.
--scope <scope>                           API key and entry scope: client, server, or all. Default: client.
--format <format>                         Output format: dotenv, json, or env-export. Default: dotenv.
--print-key                               Print the full API key. By default only the last four characters are shown.
--yes                                     Non-interactive mode; never prompt.
```

## `nona entries`

Read, write, and share config entries.

**Usage**

```text
nona entries [command] [options]
```

**Commands**

- `list` List entries in an environment.
- `get` Show one config entry.
- `history` Show version history for an entry.
- `set` Create or update an entry.
- `rollback` Restore a previous entry version.
- `delete` Delete an entry.
- `share` Create, list, and revoke entry share links.

## `nona entries list`

List entries in an environment.

**Usage**

```text
nona entries list [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
--environment <environment>               Nona environment name, for example production.
```

## `nona entries get`

Show one config entry.

**Usage**

```text
nona entries get [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
--environment <environment>               Nona environment name, for example production.
--key <key>                               Config entry key, for example Features:Checkout.
```

## `nona entries history`

Show version history for an entry.

**Usage**

```text
nona entries history [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
--environment <environment>               Nona environment name, for example production.
--key <key>                               Config entry key, for example Features:Checkout.
```

## `nona entries set`

Create or update an entry.

**Usage**

```text
nona entries set [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
--environment <environment>               Nona environment name, for example production.
--key <key>                               Config entry key, for example Features:Checkout.
--value <value>                           Value to store.
--scope <scope>                           Read scope: client, server, or all.
--content-type <content-type>             Stored value type: json, text, number, or boolean.
```

## `nona entries rollback`

Restore a previous entry version.

**Usage**

```text
nona entries rollback [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
--environment <environment>               Nona environment name, for example production.
--key <key>                               Config entry key, for example Features:Checkout.
--version <version>                       Entry version to restore.
```

## `nona entries delete`

Delete an entry.

**Usage**

```text
nona entries delete [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
--environment <environment>               Nona environment name, for example production.
--key <key>                               Config entry key, for example Features:Checkout.
```

## `nona entries share`

Create, list, and revoke entry share links.

**Usage**

```text
nona entries share [command] [options]
```

**Commands**

- `list` List share links for an entry.
- `create` Create a temporary share link.
- `revoke` Revoke a share link.

## `nona entries share list`

List share links for an entry.

**Usage**

```text
nona entries share list [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
--environment <environment>               Nona environment name, for example production.
--key <key>                               Config entry key, for example Features:Checkout.
```

## `nona entries share create`

Create a temporary share link.

**Usage**

```text
nona entries share create [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
--environment <environment>               Nona environment name, for example production.
--key <key>                               Config entry key, for example Features:Checkout.
--expiration <expiration>                 Link lifetime: 1h, 1d, 3d, 30d, or 12m.
--view-only                               Create a view-only link.
--share-base-url <share-base-url>         Browser base URL for the printed /share link. Defaults to the API base URL.
```

## `nona entries share revoke`

Revoke a share link.

**Usage**

```text
nona entries share revoke [options]
```

**Options**

```text
--api-url, --base-url <base-url>          Nona base URL.
--bearer-token, --token <bearer-token>    Admin bearer token.
--project, --project-name <project-name>  Nona project name.
--environment <environment>               Nona environment name, for example production.
--key <key>                               Config entry key, for example Features:Checkout.
--id <id>                                 Share link id to revoke.
```

## `nona config`

Show or save default CLI values.

**Usage**

```text
nona config [command] [options]
```

**Commands**

- `show` Show saved default values.
- `set <setting> <value>` Save a default base URL or project.

## `nona config show`

Show saved default values.

**Usage**

```text
nona config show [options]
```

## `nona config set`

Save a default base URL or project.

**Usage**

```text
nona config set <setting> <value> [options]
Arguments:
<setting>  Setting name: base-url or project.
<value>    Value to save as the default.
```

## `nona auth`

Sign in and manage saved sessions.

**Usage**

```text
nona auth [command] [options]
```

**Commands**

- `register` Create the first admin account and save a session.
- `login` Open a browser sign-in flow and save a session.
- `logout` Delete the saved session.
- `whoami` Show the saved session user.

## `nona auth register`

Create the first admin account and save a session.

**Usage**

```text
nona auth register [options]
```

**Options**

```text
--api-url, --base-url <base-url>  Nona base URL.
--email <email>                   Email address for the first admin.
--password <password>             Password for the first admin.
--no-save-session                 Do not save the returned session token.
```

## `nona auth login`

Open a browser sign-in flow and save a session.

**Usage**

```text
nona auth login [options]
```

**Options**

```text
--api-url, --base-url <base-url>  Nona base URL.
```

## `nona auth logout`

Delete the saved session.

**Usage**

```text
nona auth logout [options]
```

## `nona auth whoami`

Show the saved session user.

**Usage**

```text
nona auth whoami [options]
```
