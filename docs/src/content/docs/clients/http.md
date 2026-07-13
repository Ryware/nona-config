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

## Request

```bash
curl "https://nona.example.com/api/production/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"
```

Encode the key path segment. For example, `Features:Checkout` becomes `Features%3ACheckout`.

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
| `200` | Value found. |
| `401` | API key is missing or invalid. |
| `404` | Environment, key, or readable scope was not found. |

## Common troubleshooting checks

If a request fails, confirm the environment name is correct, the key exists in that environment, the key is URL-encoded, the API key belongs to the correct project, and the API key scope can read the entry scope.

## Why HTTP is still important

Even if you plan to use the JavaScript or .NET client later, HTTP is still the best first diagnostic step because it proves:

- the environment exists
- the key exists
- the API key is valid
- the public read path works
- the issue is not hidden inside client code

## When to use the official client instead

Use [JavaScript](/docs/clients/javascript/) or [.NET](/docs/clients/dotnet/) when you want:

- typed helper methods
- built-in cache behavior
- OpenFeature integration
- less manual request handling

## HTTP FAQ

### When should I use raw HTTP instead of a client?

Use raw HTTP when you want the smallest possible integration path, are working in a language without an official client, or are validating the instance during setup or migration.

### Why is the response body so simple?

The endpoint returns the raw stored value in the body and the logical type in the `X-Nona-Content-Type` header so it stays easy to consume from almost any language.

### Do I always need to URL-encode the key?

Yes.

Keys such as `Features:Checkout` must be encoded in the path, for example as `Features%3ACheckout`.

### What should I check first when a request fails?

Start with the environment name, key existence, URL encoding, API key project, and scope alignment.
