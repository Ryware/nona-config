---
title: Create your first project
description: Create a Nona project and environment so your app has a place to store remote config values.
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

Create:

- one project for your app
- one `production` environment
- one non-production environment for testing

For many teams, a simple starting structure is:

- project: `mobile-app`
- environments: `staging`, `production`

That gives you somewhere safe to test values before they affect real traffic.

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

Create the project from a terminal:

```bash
nona projects create --name storefront
nona projects list
```

Then open the admin UI and add environments there. The repo currently exposes project creation in the CLI, while environment creation is handled in the admin project screen.

## How to think about environments

Use different environments when:

- the same key should have different values in staging and production
- you want to test a feature flag before enabling it in production
- you need a safe place to validate migrated config

Avoid creating lots of environments until you actually need them. A small, clear environment model is easier to operate.

## Why this matters

Projects and environments are the base for:

- config entry reads
- API key scoping
- migration targets
- rollback and audit history

They also give you the structure you need for both major Nona use cases:

- feature flags
- broader remote config values

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

You should be able to confirm all of these:

- the project appears in `Projects`
- `staging` and `production` show up as environment tabs
- the project is available as a target for later CLI commands such as `nona entries set` and `nona keys create`

## Step-by-step summary

Use this sequence if you want the shortest reliable setup path:

1. sign in to the admin UI
2. open `Projects`
3. create the project
4. open the project
5. create `staging`
6. create `production`
7. verify both environments appear as tabs

## First project FAQ

### How many environments should I create first?

Start with two in most cases:

- one non-production environment such as `staging`
- one `production` environment

That is enough to test changes safely without creating an overly complex environment model.

### Should I create environments in the CLI or admin?

For the current documented flow, create the project in either place you prefer, then create environments in the admin UI.

That matches the current repo workflow most directly.

### Should one app get one project?

Usually yes.

A Nona project is a good boundary for one application or service and the keys, environments, API keys, and history that belong to it.

### What should I do right after the project exists?

Add your first parameter.

That is the next step that proves the project is not only created, but also ready to hold real config or feature flags.

Next: [Add your first parameter](/docs/get-started/first-parameter/)
