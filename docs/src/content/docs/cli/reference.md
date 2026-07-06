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
- Commands that connect to the Nona API may also accept `--api-url, --base-url <base-url>` and `--bearer-token, --token <bearer-token>`.
- Connection values can come from flags, `NONA_CLI_*` environment variables, saved defaults, or a matching `nona auth login` session.

## `nona`

```text
Description:
  Nona CLI for key management and Firebase Remote Config migrations.

Usage:
  nona [command] [options]

Options:
  --version       Show version information

Commands:
  users     Manage Nona users.
  projects  Manage Nona projects.
  migrate   Run config migrations.
  keys      Manage project API keys.
  entries   Manage config entries within a project environment.
  config    Manage saved CLI defaults.
  auth      Manage authentication sessions.
```

## `nona users`

```text
Description:
  Manage Nona users.

Usage:
  nona users [command] [options]

Commands:
  create  Create a new user and display their invitation token.
```

## `nona users create`

```text
Description:
  Create a new user and display their invitation token.

Usage:
  nona users create [options]

Options:
  --name <name> (REQUIRED)                Full name of the new user.
  --user-email <user-email> (REQUIRED)    Email address of the new user.
  --role <role>                           Role: viewer or editor.
  --scope <scope>                         Scope: client, server, or all.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona projects`

```text
Description:
  Manage Nona projects.

Usage:
  nona projects [command] [options]

Commands:
  list    List all projects.
  create  Create a new project.
  delete  Delete a project.
```

## `nona projects list`

```text
Description:
  List all projects.

Usage:
  nona projects list [options]
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona projects create`

```text
Description:
  Create a new project.

Usage:
  nona projects create [options]

Options:
  --name <name>                           Project name (alphanumeric and hyphens).
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona projects delete`

```text
Description:
  Delete a project.

Usage:
  nona projects delete [options]

Options:
  --project, --project-name <project-name>  Project name.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona migrate`

```text
Description:
  Run config migrations.

Usage:
  nona migrate [command] [options]

Commands:
  firebase  Migrate from Firebase Remote Config.
```

## `nona migrate firebase`

```text
Description:
  Migrate from Firebase Remote Config.

Usage:
  nona migrate firebase [options]

Options:
  --config <config>                         Path to the migration config file.
  --dry-run                                 Preview changes without applying them.
  --project, --project-name <project-name>  Project name.
  --email <email>                           Email address (forwarded to migrator).
  --password <password>                     Password (forwarded to migrator).
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona keys`

```text
Description:
  Manage project API keys.

Usage:
  nona keys [command] [options]

Commands:
  list, show  List managed API keys for a project.
  create      Generate a managed API key for a project.
  delete      Delete a managed API key.
```

## `nona keys list`

```text
Description:
  List managed API keys for a project.

Usage:
  nona keys list [options]

Options:
  --project, --project-name <project-name>  Project name.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona keys create`

```text
Description:
  Generate a managed API key for a project.

Usage:
  nona keys create [options]

Options:
  --project, --project-name <project-name>  Project name.
  --name <name>                             API key name.
  --env, --environment <environment>        Optional environment scope.
  --scope <scope>                           Config scope: client, server, or all.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona keys delete`

```text
Description:
  Delete a managed API key.

Usage:
  nona keys delete [options]

Options:
  --project, --project-name <project-name>  Project name.
  --id <id>                                 API key id.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona entries`

```text
Description:
  Manage config entries within a project environment.

Usage:
  nona entries [command] [options]

Commands:
  list      List all config entries in an environment.
  get       Get a single config entry.
  history   List version history for a config entry.
  set       Create or update a config entry.
  rollback  Roll a config entry back to a previous version.
  delete    Delete a config entry.
  share     Manage temporary parameter share links.
```

## `nona entries list`

```text
Description:
  List all config entries in an environment.

Usage:
  nona entries list [options]

Options:
  --project, --project-name <project-name>  Project name.
  --environment <environment>               Environment name.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona entries get`

```text
Description:
  Get a single config entry.

Usage:
  nona entries get [options]

Options:
  --project, --project-name <project-name>  Project name.
  --environment <environment>               Environment name.
  --key <key>                               Config entry key.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona entries history`

```text
Description:
  List version history for a config entry.

Usage:
  nona entries history [options]

Options:
  --project, --project-name <project-name>  Project name.
  --environment <environment>               Environment name.
  --key <key>                               Config entry key.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona entries set`

```text
Description:
  Create or update a config entry.

Usage:
  nona entries set [options]

Options:
  --project, --project-name <project-name>  Project name.
  --environment <environment>               Environment name.
  --key <key>                               Config entry key.
  --value <value>                           The config value.
  --scope <scope>                           Scope: client, server, or all.
  --content-type <content-type>             Logical content type: json, text, number, or boolean.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona entries rollback`

```text
Description:
  Roll a config entry back to a previous version.

Usage:
  nona entries rollback [options]

Options:
  --project, --project-name <project-name>  Project name.
  --environment <environment>               Environment name.
  --key <key>                               Config entry key.
  --version <version>                       Version number to roll back to.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona entries delete`

```text
Description:
  Delete a config entry.

Usage:
  nona entries delete [options]

Options:
  --project, --project-name <project-name>  Project name.
  --environment <environment>               Environment name.
  --key <key>                               Config entry key.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona entries share`

```text
Description:
  Manage temporary parameter share links.

Usage:
  nona entries share [command] [options]

Commands:
  list    List share links for a config entry.
  create  Create a temporary share link for a config entry.
  revoke  Revoke a temporary share link.
```

## `nona entries share list`

```text
Description:
  List share links for a config entry.

Usage:
  nona entries share list [options]

Options:
  --project, --project-name <project-name>  Project name.
  --environment <environment>               Environment name.
  --key <key>                               Config entry key.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona entries share create`

```text
Description:
  Create a temporary share link for a config entry.

Usage:
  nona entries share create [options]

Options:
  --project, --project-name <project-name>  Project name.
  --environment <environment>               Environment name.
  --key <key>                               Config entry key.
  --expiration <expiration>                 Expiration: 1h, 1d, 3d, 30d, or 12m.
  --view-only                               Create a view-only link instead of an editable link.
  --share-base-url <share-base-url>         Base URL for the printed browser link; defaults to the API base URL.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona entries share revoke`

```text
Description:
  Revoke a temporary share link.

Usage:
  nona entries share revoke [options]

Options:
  --project, --project-name <project-name>  Project name.
  --environment <environment>               Environment name.
  --key <key>                               Config entry key.
  --id <id>                                 Share link id to revoke.
```

Also accepts: `--api-url, --base-url <base-url>`, `--bearer-token, --token <bearer-token>`.

## `nona config`

```text
Description:
  Manage saved CLI defaults.

Usage:
  nona config [command] [options]

Commands:
  show                   Show saved defaults.
  set <setting> <value>  Save a CLI default.
```

## `nona config show`

```text
Description:
  Show saved defaults.

Usage:
  nona config show [options]
```

## `nona config set`

```text
Description:
  Save a CLI default.

Usage:
  nona config set <setting> <value> [options]

Arguments:
  <setting>  Setting to configure: base-url, project.
  <value>    The new value.
```

## `nona auth`

```text
Description:
  Manage authentication sessions.

Usage:
  nona auth [command] [options]

Commands:
  login   Open a browser to log in and save a session.
  logout  Remove saved session.
  whoami  Show current session info.
```

## `nona auth login`

```text
Description:
  Open a browser to log in and save a session.

Usage:
  nona auth login [options]
```

Also accepts: `--api-url, --base-url <base-url>`.

## `nona auth logout`

```text
Description:
  Remove saved session.

Usage:
  nona auth logout [options]
```

## `nona auth whoami`

```text
Description:
  Show current session info.

Usage:
  nona auth whoami [options]
```
