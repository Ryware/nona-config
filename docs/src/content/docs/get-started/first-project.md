---
title: Create your first project
description: Create a Nona project and environment so your app has a place to store and read remote config values and feature flags.
---

A Nona project groups related config for one app or service.

Inside a project, you create environments such as:

- `development`
- `staging`
- `production`

Think of the project as the boundary for:

- your app's config entries
- the API keys that can read them
- the environments that separate test and production values
- the audit and rollback history for that app

## Suggested first setup

Start with one project for your app, one `production` environment, and one non-production environment such as `staging`. For many teams, `storefront` with `staging` and `production` is enough to get real separation without adding unnecessary complexity.

## In admin

1. sign in to the admin UI
2. open `Projects`
3. create a project if it does not exist yet
4. open the project
5. click `Add Environment`
6. create `staging`
7. click `Add Environment` again
8. create `production`

Once an environment exists, it appears as a selectable environment tab on the project page.

## With the CLI

For a fresh setup, create the first project and `production` environment with `init`:

```bash
nona init --yes --base-url https://nona.example.com --email admin@example.com --password <password> --project storefront
```

For later project-only administration, create the project from a terminal:

```bash
nona projects create --name storefront
nona projects list
```

Then open the admin UI and add any extra environments there.

## How to think about environments

Use different environments when:

- the same key should have different values in staging and production
- you want to test a feature flag before enabling it in production
- you need a safe place to validate migrated config

Avoid creating lots of environments until you actually need them. A small, clear environment model is easier to operate.

## Why this matters

Projects and environments are the base for config entry reads, API key scoping, migration targets, rollback, audit history, feature flags, and broader remote config values.

## A concrete example

You might start with:

- project: `storefront`
- environment: `production`
- key: `Features:Checkout`

Later, that same project might also hold:

- `Checkout:BannerText`
- `Checkout:MaxItems`
- `Checkout:Settings`

That is why the project/environment layer comes first. Everything else builds on it.

## Validate the setup

Confirm the project appears in `Projects`, `staging` and `production` show up as environment tabs, and the project is available as a later CLI target for commands such as `nona entries set` and `nona keys create`.

## First project FAQ

### How many environments should I create first?

Start with two in most cases:

- one non-production environment such as `staging`
- one `production` environment

That is enough to test changes safely without creating an overly complex environment model.

### Should I create environments in the CLI or admin?

Use `nona init` for the first automated project and environment. For additional environments, use the admin UI.

That matches the current repo workflow most directly.

### Should one app get one project?

Usually yes.

A Nona project is a good boundary for one application or service and the keys, environments, API keys, and history that belong to it.

### What should I do right after the project exists?

Add your first parameter.

That is the next step that proves the project is not only created, but also ready to hold real config or feature flags.

Next: [Add your first parameter](/docs/get-started/first-parameter)
