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

### Windows via WinGet

Once the package is approved on Windows Package Manager Community Repository:

```powershell
winget install Ryware.NonaCLI
```

The WinGet package installs the Windows x64 or Windows ARM64 release asset automatically based on the local machine architecture.

### Local Build

```bash
dotnet publish cli/src/Nona.Cli/Nona.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

You can swap `win-x64` for another runtime identifier such as `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64`.

The published executable is the `nona` command.

## Commands

Authenticate and persist a session token:

```bash
nona auth login --base-url http://nona.internal:18080 --email admin@example.com
nona auth whoami
nona auth logout
```

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

Resolution order is:

1. Explicit command-line flags
2. `NONA_CLI_*` environment variables
3. Saved auth session from `nona auth login` for bearer token reuse
4. Saved CLI defaults from `nona config set`
