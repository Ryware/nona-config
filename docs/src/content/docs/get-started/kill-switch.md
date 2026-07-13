---
title: Add a kill switch
description: Use a boolean Nona parameter as a kill switch so you can disable risky behavior without redeploying.
---

A kill switch is one of the simplest and most valuable feature flag patterns.

## Basic pattern

Create a boolean entry such as:

- key: `Features:Checkout`
- value: `true`
- scope: `client` or `all`

When something goes wrong, set it to `false`.

For backend-only behavior, use `server` scope instead.

## Why this is useful

- no app redeploy required
- one fast operational escape hatch
- easy to audit and roll back

## Good first kill switch candidates

- new checkout logic
- a risky third-party integration
- a heavy background process
- a new navigation or onboarding flow

The best kill switches guard code paths that are valuable to disable quickly under real production pressure.

## In admin

1. open `Projects`
2. open the target project
3. select the environment you want to protect, usually `production`
4. click `Add Parameter`
5. create a boolean parameter such as `Features:Checkout`
6. set the initial value to `true`
7. choose `client`, `server`, or `all` based on where the flag is evaluated
8. click `Create`

When you need to disable the feature later:

1. click the parameter row
2. stay on the `Settings` tab
3. change the boolean value to `false`
4. click `Save`

To review or undo a change:

1. open the same parameter
2. switch to the `History` tab
3. click `Rollback to v...` on the version you want to restore

## With the CLI

Create the kill switch:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value true \
  --scope client \
  --content-type boolean
```

Disable it during an incident:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value false \
  --scope client \
  --content-type boolean
```

Inspect history and roll back if needed:

```bash
nona entries history --project storefront --environment production --key Features:Checkout
nona entries rollback --project storefront --environment production --key Features:Checkout --version 2
```

## What a good kill switch does

A good kill switch should:

- be easy to understand
- default to a safe behavior when off
- be tested in both states
- be documented before the incident happens

If the application only works correctly in the `true` path, it is not really ready to benefit from a kill switch yet.

## Step-by-step kill switch summary

Use this sequence for the fastest first kill switch:

1. create a boolean parameter such as `Features:Checkout`
2. set the initial value to `true`
3. choose the correct scope for where the flag is evaluated
4. wire the app to respect both `true` and `false`
5. test the off path before an incident happens
6. flip it to `false` when you need to disable the feature

## Kill switch FAQ

### What is the best first kill switch candidate?

A risky but easy-to-disable feature path is usually best, such as new checkout logic or a third-party integration.

### Should a kill switch always be boolean?

Usually yes.

Boolean values are the clearest fit for kill switches because the operational action is typically just on or off.

### Should the kill switch be `client` or `server`?

It depends on where the app evaluates the flag.

Use `client` for frontend or mobile checks, `server` for backend-only behavior, and `all` only when both sides genuinely need to read it.

### What makes a kill switch operationally useful?

The off path must actually be safe and tested.

If disabling the flag still breaks the feature or the application, the kill switch is not doing the job you need during an incident.

Related docs:

- [Feature flags vs remote config](/docs/feature-flags/feature-flags-vs-remote-config/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [History and rollback](/docs/concepts/history-and-rollback/)
