---
title: HTTP
description: Fetch one Nona config value with an API key.
---

Use HTTP when an app needs one value and does not use a client package.

```http
GET /api/{environmentId}/{key}
X-Api-Key: <api-key>
```

The API key is bound to one project. The request only includes the environment and key.

This makes HTTP the smallest possible integration path for:

- backend services
- scripts
- languages without an official client
- quick validation during setup or migration

## Request

```bash
curl "https://nona.example.com/api/production/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"
```

Encode the key path segment. For example, `Features:Checkout` becomes `Features%3ACheckout`.

## Response

The response body is the stored value.

```text
true
```

Nona also returns the logical value type:

```http
X-Nona-Content-Type: boolean
```

Supported logical types are:

- `text`
- `number`
- `boolean`
- `json`

## Why the response is simple

The HTTP endpoint returns the raw stored value in the body and the logical type in the `X-Nona-Content-Type` header.

That keeps the endpoint easy to use from almost any language:

- read the body as text
- inspect the header if you need to interpret the type

For example:

- `true` plus `X-Nona-Content-Type: boolean`
- `42` plus `X-Nona-Content-Type: number`
- a JSON string plus `X-Nona-Content-Type: json`

## Status codes

| Status | Meaning |
|---|---|
| `200` | Value found. |
| `401` | API key is missing or invalid. |
| `404` | Environment, key, or readable scope was not found. |

## Common troubleshooting checks

If a request fails:

1. confirm the environment name is correct
2. confirm the key exists in that environment
3. confirm the key is URL-encoded
4. confirm the API key belongs to the correct project
5. confirm the API key scope can read the entry scope

## Setup checklist

Before calling the endpoint:

1. Create a project in the Nona admin UI.
2. Create an environment, for example `production`.
3. Create a config entry, for example `Features:Checkout`.
4. Create an API key with a scope that can read the entry.
5. Store the API key in your app's secrets, not in source code.

## When to use the official client instead

Use [JavaScript](/docs/clients/javascript/) or [.NET](/docs/clients/dotnet/) when you want:

- typed helper methods
- built-in cache behavior
- OpenFeature integration
- less manual request handling
