---
title: Add your first parameter
description: Create your first Nona config entry and choose the right content type and scope.
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

If you want the simplest possible first test, start with one of these:

- `Features:Checkout` = `true` with content type `boolean`
- `App:BannerText` = `Hello` with content type `text`

If your app is frontend-facing, `client` is usually the easiest first scope.

If the value should only exist on the server, use `server` from the start instead of widening it later.

## Common mistakes

- storing a feature flag as `text` instead of `boolean`
- using `all` when only the backend should read the value
- putting unrelated settings into one large JSON blob too early

Keep the first parameter small and easy to verify. You can expand the shape later once the read path is working.

Next: [Create an API key](/docs/get-started/api-keys/)
