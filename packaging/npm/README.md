# nona-cli

Command-line interface for **Nona** — an open-source, self-hosted remote configuration and feature flag service. Nona is a Firebase Remote Config alternative you run yourself: one REST API, any language, Docker-first, and Apache 2.0 licensed.

Use the CLI to manage projects, environments, config entries, and feature flags from your terminal — and to **migrate from Firebase Remote Config** with a single command.

- Website: https://nonaconfig.com
- Source & documentation: https://github.com/Ryware/nona-config

## Install

```bash
npm install -g nona-cli
```

On Windows you can also install via Chocolatey:

```bash
choco install nona-cli
```

Both install the `nona` command.

## Authenticate

```bash
nona auth login     # open a browser to log in and save a session
nona auth whoami    # show the current session
nona auth logout    # remove the saved session
```

## Projects

```bash
nona projects list
nona projects create
```

## Config entries and feature flags

Config entries are key-value pairs scoped to a project environment. A feature flag is simply an entry with a boolean value.

```bash
# List every entry in an environment
nona entries list --project my-app --environment production

# Read one entry
nona entries get --project my-app --environment production --key checkout_v2

# Create or update an entry (a feature flag here)
nona entries set --project my-app --environment production --key checkout_v2 --value true

# Delete an entry
nona entries delete --project my-app --environment production --key checkout_v2
```

`entries set` also accepts `--scope` (`client`, `server`, or `all`) and `--content-type`.

## API keys

```bash
nona keys list --project my-app
nona keys create --project my-app
```

## Migrate from Firebase Remote Config

```bash
nona migrate firebase
```

Imports your existing Firebase Remote Config parameters into Nona, so you can move off the hosted service without re-creating your configuration by hand.

## Saved defaults

Avoid repeating common flags by saving defaults:

```bash
nona config set
nona config show
```

Run `nona --help` or `nona <command> --help` for the full list of commands and options.

## Requirements

- Node.js 18 or newer

## License

Apache-2.0 © [Ryware](https://ryware.dev)
