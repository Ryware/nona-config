---
title: Parameters and content types
description: Learn how Nona stores parameters, which content types it supports, and how to pick the right shape for each value.
---

Nona stores config as entries keyed per environment.

Each entry has:

- a key
- a value
- a content type
- a scope

This is the core building block behind both major Nona use cases:

- feature flags
- broader remote config

## Supported content types

- `text`
- `number`
- `boolean`
- `json`

## When to use each type

### `boolean`

Use `boolean` for:

- feature flags
- kill switches
- on/off rollout gates

### `text`

Use `text` for:

- labels and copy
- simple string values
- identifiers that do not need to be parsed as numbers or JSON

### `number`

Use `number` for:

- limits
- thresholds
- percentages
- retry counts

### `json`

Use `json` when multiple related values belong together, for example:

- structured module settings
- grouped UI options
- objects a client can deserialize directly

## Practical examples

| Key | Example value | Good content type |
|---|---|---|
| `Features:Checkout` | `true` | `boolean` |
| `App:BannerText` | `Hello` | `text` |
| `Checkout:MaxItems` | `50` | `number` |
| `Checkout:Settings` | `{"color":"green","enabled":true}` | `json` |

## How to create one

In admin:

1. open `Projects`
2. open the project
3. select the environment
4. click `Add Parameter`
5. enter the key and value
6. pick the content type that matches the value shape
7. pick the scope
8. click `Create`

With the CLI:

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Checkout:MaxItems \
  --value 50 \
  --scope server \
  --content-type number
```

## Choosing between separate keys and JSON

Use separate keys when:

- values change independently
- you want smaller, clearer reads
- a single setting is operationally important on its own

Use JSON when:

- the values belong together
- the client naturally consumes them as one object
- keeping the structure together makes the configuration easier to reason about

## Scope still matters

Content type and scope are different decisions.

For example:

- a `boolean` flag can be `client`, `server`, or `all`
- a `json` settings object can also be `client`, `server`, or `all`

Choose the content type based on the value shape, then choose scope based on who should read it.

## Easy mistakes to avoid

- using `text` for a real boolean flag
- putting unrelated values into one large JSON object
- using `json` just to avoid creating separate keys
- choosing `all` scope when only the backend needs the value

## FAQ

### What is the best first parameter type to create?

Usually a `boolean` flag or a simple `text` value.

Those are the easiest shapes to validate during the first integration.

### When should I use `json` instead of separate keys?

Use `json` when the values naturally belong together and the client consumes them as one structured object.

### Does content type control who can read the value?

No.

Content type describes the value shape. Scope controls who can read it.

### What is the most common datatype mistake?

Storing a real feature flag as `text` instead of `boolean`.

That makes the application logic less clear and weakens the feature-flag model.

## Related docs

- [Client vs server scope](/docs/concepts/client-vs-server-scope)
- [Feature flags](/docs/feature-flags)
- [Remote config](/docs/remote-config)
