---
title: HTTP
description: Fetch one or all client-visible Nona config values over plain HTTP with an API key, from any language or platform, with no SDK.
---

Use HTTP when an app needs config values and does not use a client package.

```http
GET /api/{environmentId}/{key}
X-Api-Key: <api-key>
```

The API key is bound to one project. The request only includes the environment and key. Without a `version` query parameter, Nona reads the environment's active release.

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
6. publish a release and set it active
7. create an API key in the `API Keys` section
8. keep the key scope aligned with the entry scope

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

Then publish and activate a release for the environment in admin.

## Request

```bash
curl "https://nona.example.com/api/production/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"
```

Encode the key path segment. For example, `Features:Checkout` becomes `Features%3ACheckout`.

To pin a client to a release, add `version`:

```bash
curl "https://nona.example.com/api/production/Features%3ACheckout?version=1.1.0" \
  -H "X-Api-Key: $NONA_API_KEY"

curl "https://nona.example.com/api/production/Features%3ACheckout?version=1.1.x" \
  -H "X-Api-Key: $NONA_API_KEY"
```

`1.1.0` resolves exactly. `1.1.x` resolves to the highest patch in the `1.1` release line.

## Fetch all client-visible values

Use the environment-only route to fetch the complete client snapshot in one request:

```bash
curl -i "https://nona.example.com/api/production" \
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

The bulk route also accepts `?version=1.1.0` and `?version=1.1.x`.

### Conditional polling with ETag

Every successful bulk response includes an `ETag`. Send it back in `If-None-Match` when polling:

```bash
curl -i "https://nona.example.com/api/production" \
  -H "X-Api-Key: $NONA_API_KEY" \
  -H 'If-None-Match: "<etag-from-the-previous-response>"'
```

If the client-visible snapshot has not changed, Nona returns `304 Not Modified` with no response body. Server-only entry changes do not change this client snapshot ETag.

If you want to see the response headers too:

```bash
curl -i "https://nona.example.com/api/production/Features%3ACheckout" \
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
| `404` | Environment, active release, requested release, key, or readable scope was not found. |

## Common troubleshooting checks

If a request fails:

1. confirm the environment name is correct
2. confirm the key exists in that environment
3. confirm the key is URL-encoded
4. confirm the environment has an active release, or pass `version`
5. confirm the API key belongs to the correct project
6. confirm the API key scope can read the entry scope

## Setup checklist

Before calling the endpoint:

1. Create a project in the Nona admin UI.
2. Create an environment, for example `production`.
3. Create a config entry, for example `Features:Checkout`.
4. Publish a release and set it active.
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
