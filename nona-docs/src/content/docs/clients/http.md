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

## Status codes

| Status | Meaning |
|---|---|
| `200` | Value found. |
| `401` | API key is missing or invalid. |
| `404` | Environment, key, or readable scope was not found. |

## Setup checklist

Before calling the endpoint:

1. Create a project in the Nona admin UI.
2. Create an environment, for example `production`.
3. Create a config entry, for example `Features:Checkout`.
4. Create an API key with a scope that can read the entry.
5. Store the API key in your app's secrets, not in source code.
