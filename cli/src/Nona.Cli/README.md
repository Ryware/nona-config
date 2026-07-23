# Nona CLI

`Nona.Cli` packages the Nona admin and migration workflows as a standalone cross-platform CLI.

## Install

Download the archive for your platform from GitHub Releases, extract it, and put the `nona` binary on your `PATH`.

Each release archive also includes an `appsettings.json` sample for the Firebase migration workflow.

Release assets are published for:

- Windows x64
- Windows ARM64
- Linux x64
- Linux ARM64
- macOS x64
- macOS ARM64

Archive naming:

- `nona-cli_<version>_win-x64.zip`
- `nona-cli_<version>_linux-x64.tar.gz`
- `nona-cli_<version>_osx-arm64.tar.gz`

### Windows via Chocolatey

Once the package is approved on Chocolatey Community Repository:

```powershell
choco install nona-cli
```

The Chocolatey package installs the Windows x64 or Windows ARM64 release asset automatically based on the local machine architecture.

### Local Build

```bash
dotnet publish cli/src/Nona.Cli/Nona.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

You can swap `win-x64` for another runtime identifier such as `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64`.

The published executable is the `nona` command.

## Commands

Bootstrap a fresh Nona instance and print app-ready environment variables:

```bash
nona init --yes --base-url http://nona.internal:18080 --email admin@example.com --password secret --project mobile-app --print-key
```

`init` registers or logs in the admin, creates or reuses the project and environment, seeds a starter flag, creates or reuses a scoped API key, and prints a `.env` block plus a verification curl.

`--yes` makes the command non-interactive: it never prompts and fails fast if a required value is missing.

Authenticate and persist a session token:

```bash
nona auth register --base-url http://nona.internal:18080 --email admin@example.com --password secret
nona auth login --base-url http://nona.internal:18080
nona auth whoami
nona auth logout
```

`auth register` is only for first-time setup. It creates the initial admin account through `/auth/register` and saves the returned session token for follow-up CLI commands.

Set saved defaults for repeated use:

```bash
nona config set base-url http://nona.internal:18080
nona config set project mobile-app
nona config show
```

Show project API keys:

```bash
nona keys show --project mobile-app --base-url https://nona.example.com --token <token>
```

Create a scoped API key:

```bash
nona keys create --project mobile-app --name "Web Client" --scope client --environment production --base-url https://nona.example.com --token <token>
```

Manage config entries:

```bash
nona entries list --project mobile-app --environment production --base-url https://nona.example.com --token <token>
nona entries get --project mobile-app --environment production --key welcome_text --base-url https://nona.example.com --token <token>
nona entries set --project mobile-app --environment production --key welcome_text --value "Hello" --scope all --content-type text --base-url https://nona.example.com --token <token>
nona entries history --project mobile-app --environment production --key welcome_text --base-url https://nona.example.com --token <token>
nona entries rollback --project mobile-app --environment production --key welcome_text --version 2 --base-url https://nona.example.com --token <token>
```

Manage immutable releases:

```bash
nona releases list --project mobile-app --environment production
nona releases list --project mobile-app --environment production --json
nona releases view 1.1.0 --project mobile-app --environment production
nona releases view 1.1.0 --project mobile-app --environment production --json
nona releases create 1.2 --project mobile-app --environment production
nona releases amend 1.1.0 --project mobile-app --environment production --set feature.checkout=false
nona releases activate 1.2.0 --project mobile-app --environment production
nona releases clear-active --project mobile-app --environment production
nona releases delete 1.1.0 --project mobile-app --environment production
```

`releases create` accepts `major.minor`, stores it as patch `.0`, and snapshots the current working configuration. Add `--activate` to make the new release active immediately.

`releases amend` calculates the next patch in the source release's line, edits a local copy, and publishes the complete edited entries payload without changing the working configuration. Choose one edit mode:

- repeat `--set key=value` and `--delete key` for direct changes
- use `--from-file ./entries.json` with a JSON array of `{ "key", "value", "contentType", "scope" }` objects
- use `--editor` to open formatted JSON with `VISUAL`, falling back to `EDITOR`

When run interactively with no edit option, amend defaults to the editor. Non-interactive use must select an edit mode. Release-management commands use exact `major.minor.patch` versions; `major.minor.x` is supported only by client configuration reads.

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
- `NONA_INIT_EMAIL`
- `NONA_INIT_PASSWORD`

The Firebase migration command also continues to support the existing `NONA_MIGRATOR_*` environment variables.

Resolution order is:

1. Explicit command-line flags
2. `NONA_CLI_*` environment variables
3. Saved CLI defaults from `nona config set`
4. Saved auth session from `nona auth login` for base URL and bearer token reuse

## HTTP/API error output and exit codes

HTTP/API failures are written to standard error as one human-readable line, including the HTTP status and the server's error code when available:

```text
Error: value is not a valid number (400, INVALID_VALUE)
```

Pass the global `--verbose` option to include the full exception and stack trace for debugging. Without `--verbose`, stack traces are suppressed.

| Exit code | HTTP/API failure |
| --- | --- |
| `2` | Validation or other client request error (`400`, `422`, or another `4xx`) |
| `3` | Authentication or authorization error (`401` or `403`) |
| `4` | Resource not found (`404`) |
| `5` | Conflict (`409`) |
| `6` | Server error (`5xx`) |

Other command-specific failures may use different non-zero exit codes.
