---
title: HTTP
description: Fetch one or all client-visible Nona config values over plain HTTP with an API key, from any language or platform, with no SDK.
---

Use HTTP when an app needs config values and does not use a client package.

```http
GET /api/environments/{environmentId}/parameters/{key}
X-Api-Key: <api-key>
```

The API key is bound to one project. The route explicitly chooses working parameters, the active release, or a selected immutable release. Active-release routes return `409 active_release_not_configured` when no active release exists; they never fall back to working parameters.

Important scope note: this endpoint does not evaluate per-user context. Parameters or headers such as `userId`, `X-User-Id`, segments, cohorts, or percentage-rollout hints are not part of the Nona HTTP read model.

This makes HTTP the smallest possible integration path for:

- backend services
- scripts
- languages without an official client
- quick validation during setup or migration

## Prepare the value in admin

1. open `Projects`
2. open the project
3. create the target environment such as `production`
4. click `Add Parameter`
5. create a key such as `Features:Checkout`
6. create an API key in the `API Keys` section
7. keep the key scope aligned with the entry scope

## Prepare the value with the CLI

```bash
nona entries set \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --value true \
  --scope client \
  --content-type boolean

nona keys create \
  --project storefront \
  --name "HTTP smoke test" \
  --scope client \
  --environment production
```

The first request below uses working parameters, so no release is required. Publish a release only for a release-route request; set it active only when using the `/releases/active/` route.

## Request

```bash
curl "https://nona.example.com/api/environments/production/parameters/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"
```

Encode the key path segment. For example, `Features:Checkout` becomes `Features%3ACheckout`.

To pin a client to a release, put the exact or wildcard selector in the route:

```bash
curl "https://nona.example.com/api/environments/production/releases/1.1.0/parameters/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"

curl "https://nona.example.com/api/environments/production/releases/1.1.x/parameters/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"
```

`1.1.0` resolves exactly. `1.1.x` resolves to the highest patch in the `1.1` release line.

These versioned requests require the requested release or matching release line to exist, but they do not require an active release. Use `/api/environments/production/releases/active/parameters/{key}` to read the active release; if none is active, the server returns `409 active_release_not_configured`. Release reads never fall back to working parameters.

## Fetch all client-visible values

Use the bulk working route to fetch the complete client-visible working snapshot in one request:

```bash
curl -i "https://nona.example.com/api/environments/production/parameters" \
  -H "X-Api-Key: $NONA_API_KEY"
```

The API key must have `client` or `all` scope. The response includes entries with `client` or `all` scope and always excludes server-only entries. A valid server-only key receives `404`, matching the single-key endpoint's behavior for an unreadable scope.

```json
{
  "Features:Checkout": {
    "value": "true",
    "contentType": "boolean"
  },
  "App:Banner": {
    "value": "Welcome",
    "contentType": "text"
  }
}
```

To fetch a release snapshot, use `/api/environments/production/releases/active/parameters` for the active release, or `/api/environments/production/releases/{version}/parameters` with an exact selector such as `1.1.0` or a line such as `1.1.x`.

Add an optional `prefix` to fetch only keys that start with a group name:

```bash
curl -i "https://nona.example.com/api/environments/production/parameters?prefix=GroupA%3A" \
  -H "X-Api-Key: $NONA_API_KEY"
```

Prefix matching is case-insensitive: `GroupA:` also matches `groupa:Flag`. A non-empty prefix may contain only ASCII letters, digits, colons, dots, underscores, and dashes; it remains a fragment, so a trailing colon is valid. Any other character returns `400 Bad Request` with Problem Details before entries or ETags are evaluated. Omit `prefix`, or pass an empty value, for the complete snapshot. A valid prefix with no matches returns `200` with `{}` and a valid prefix-specific ETag. `prefix` works on working, active-release, and selected-release collection routes.

### Conditional polling with ETag

Every successful bulk response includes an `ETag`. Send it back in `If-None-Match` when polling:

```bash
curl -i "https://nona.example.com/api/environments/production/parameters" \
  -H "X-Api-Key: $NONA_API_KEY" \
  -H 'If-None-Match: "<etag-from-the-previous-response>"'
```

If the client-visible snapshot has not changed, Nona returns `304 Not Modified` with no response body. Server-only entry changes do not change this client snapshot ETag. Working and release sources have isolated ETag identities. Each non-empty prefix has its own ETag identity, while casing variants of the same prefix share an ETag. An ETag from another source, selector, unfiltered request, or different prefix cannot produce `304` for the current request.

If you want to see the response headers too:

```bash
curl -i "https://nona.example.com/api/environments/production/parameters/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"
```

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
| `200` | Value or bulk snapshot found. |
| `304` | Bulk snapshot is unchanged for the supplied `If-None-Match` value. |
| `401` | API key is missing or invalid. |
| `404` | Environment, requested release, key, or readable scope was not found. |
| `409` | The active-release route was requested and no active release is configured. |

## Common troubleshooting checks

If a request fails:

1. confirm the environment name is correct
2. confirm the key exists in that environment
3. confirm the key is URL-encoded
4. confirm that you chose the intended working or release route
5. for a release route, confirm the expected release is active or put an exact or wildcard selector in the route
6. confirm the API key belongs to the correct project
7. confirm the API key scope can read the entry scope

## Setup checklist

Before calling the endpoint:

1. Create a project in the Nona admin UI.
2. Create an environment, for example `production`.
3. Create a config entry, for example `Features:Checkout`.
4. If using a release route, publish the requested release. Set it active only when using the active-release route.
5. Create an API key with a scope that can read the entry.
6. Store the API key in your app's secrets, not in source code.

## Why HTTP is still important

Even if you plan to use the JavaScript or .NET client later, HTTP is still the best first diagnostic step because it proves:

- the environment exists
- the key exists
- the API key is valid
- the public read path works
- the issue is not hidden inside client code

## When to use the official client instead

Use [JavaScript](/docs/clients/javascript) or [.NET](/docs/clients/dotnet) when you want:

- typed helper methods
- built-in cache behavior
- one-call cache priming
- OpenFeature integration
- less manual request handling
