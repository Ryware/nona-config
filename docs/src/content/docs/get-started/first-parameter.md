---
title: Add your first parameter
description: Create your first Nona config entry, choose the right content type and scope, and set a value your app can read at runtime.
---

A parameter in Nona is a config entry stored in a project environment.

Good first examples:

- `Features:Checkout` with value `true`
- `App:BannerText` with value `Hello`
- `App:Settings` with a JSON object

Those examples show the two main sides of Nona:

- feature flags through boolean values
- remote config through text, numeric, and JSON values

## Choose the right content type

Nona supports:

- `text`
- `number`
- `boolean`
- `json`

Use them like this:

- `boolean` for feature flags and kill switches
- `text` for copy, labels, or simple string settings
- `number` for thresholds, percentages, and limits
- `json` for structured configuration that belongs together

## Choose the right scope

- `client` for frontend/mobile-readable values
- `server` for backend-only values
- `all` for values both sides can read

Scope is one of the most important Nona decisions because it affects what kind of API key can read the entry.

## Good first parameter choices

For the simplest first test, start with `Features:Checkout = true` as a `boolean` or `App:BannerText = Hello` as `text`. If your app is frontend-facing, `client` is usually the easiest first scope. If the value should stay backend-only, use `server` from the start.

## In admin

1. open `Projects`
2. open your project
3. select the target environment tab such as `production`
4. click `Add Parameter`
5. enter a key such as `Features:Checkout`
6. pick `boolean` as the datatype
7. set the scope to `client` or `server`
8. enter the value
9. click `Create`

After creation, the parameter appears in the table for the active environment.

## With the CLI

Create the same entry from a terminal:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value true \
  --scope client \
  --content-type boolean
```

Then verify it:

```bash
nona entries get --project storefront --environment production --key Features:Checkout
nona entries list --project storefront --environment production
```

If you already saved the project with `nona config set project storefront`, you can omit `--project`.

## Common mistakes

- storing a feature flag as `text` instead of `boolean`
- using `all` when only the backend should read the value
- putting unrelated settings into one large JSON blob too early

Keep the first parameter small and easy to verify. You can expand the shape later once the read path is working.

## First parameter FAQ

### What is the best first parameter to create?

A boolean flag such as `Features:Checkout` is usually the easiest first choice.

It is simple to verify and immediately demonstrates the feature-flag side of Nona.

### When should I use `boolean` instead of `text`?

Use `boolean` when the value is really acting as a flag or kill switch.

If the value is freeform content or a label, use `text` instead.

### Should I use `client`, `server`, or `all` first?

Use the narrowest scope that matches the real read surface.

For many frontend or mobile tests, `client` is the easiest first scope. For backend-only values, use `server`.

### Should I start with a JSON value?

Usually no.

A simple boolean or text value is easier to validate first. Add JSON once the basic read path is already working.

Next: [Create an API key](/docs/get-started/api-keys)
