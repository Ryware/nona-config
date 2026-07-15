---
title: Fetch your first config value
description: Make your first Nona read over HTTP with an API key and a single environment/key request.
---

Once you have:

- a project
- an environment
- a config entry
- an API key

you can read a value over HTTP.

The shortest way to prepare those pieces is:

```bash
nona init --yes --base-url https://nona.example.com --email admin@example.com --password <password> --project storefront --print-key
```

The command prints the environment id, API key, and a verification curl for the seeded `Features:Example` flag.

## What to prepare first

In admin:

1. open `Projects`
2. open the project
3. select the target environment
4. make sure the parameter exists
5. create an API key in the `API Keys` section

For the simplest first test, use a boolean key such as `Features:Checkout`.

With the CLI, that setup can look like:

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
  --name "HTTP test" \
  --scope client \
  --environment production
```

## Request shape

```http
GET /api/{environmentId}/{key}
X-Api-Key: <api-key>
```

The request includes:

- the environment id
- the config key
- an API key in the header

The project is implied by the API key, which is why it is not part of this request path.

## Example

```bash
curl "https://nona.example.com/api/production/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"
```

If you want to inspect the response headers too:

```bash
curl -i "https://nona.example.com/api/production/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"
```

The key path segment must be URL-encoded. For example:

- `Features:Checkout` -> `Features%3ACheckout`

## What a successful first read proves

If this request works, you have validated that the Nona instance is reachable, the environment and key exist, the API key is valid, and the API key scope can read the entry. That makes it the best milestone before you integrate one of the official clients.

## Fast troubleshooting

If the request fails:

1. confirm the environment name is correct
2. confirm the key exists in that environment
3. confirm the key path is URL-encoded
4. confirm the API key belongs to the same project
5. confirm the API key scope can read the entry scope

## First API call FAQ

### Why is the project name not in the HTTP path?

The API key already scopes the request to a project.

That is why the request path only needs the environment id and key.

### Do I need to URL-encode the key?

Yes.

Keys such as `Features:Checkout` must be URL-encoded in the path, for example as `Features%3ACheckout`.

### Should I test over HTTP before using an SDK?

Yes, in most cases.

A direct HTTP read is the simplest way to prove the instance, key, environment, and API key are all aligned before you add SDK code.

### What should I do after the first successful read?

Either keep using direct HTTP for a very small integration, or move to the JavaScript or .NET client for application code.

## What to do next

After the first direct HTTP read, most teams either keep using [HTTP](/docs/clients/http/) for a very small integration, switch to [JavaScript](/docs/clients/javascript/) or [.NET](/docs/clients/dotnet/) for application code, or add a [kill switch](/docs/get-started/kill-switch/) as the first operational flag. For full endpoint behavior, see [HTTP](/docs/clients/http/).

Next: [Add a kill switch](/docs/get-started/kill-switch/)
