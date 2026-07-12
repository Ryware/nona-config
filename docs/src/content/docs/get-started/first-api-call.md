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

The key path segment must be URL-encoded. For example:

- `Features:Checkout` -> `Features%3ACheckout`

## What a successful first read proves

If this request works, you have already validated a lot:

- the Nona instance is reachable
- the environment exists
- the key exists
- the API key is valid
- the API key scope can read the entry

That is why this step is a good milestone before you integrate one of the official clients.

## What to do next

After the first direct HTTP read, most teams choose one of these paths:

- keep using [HTTP](/docs/clients/http/) for a very small integration
- switch to [JavaScript](/docs/clients/javascript/) or [.NET](/docs/clients/dotnet/) for app code
- add a [kill switch](/docs/get-started/kill-switch/) as the first operational flag

For the full endpoint behavior, see [HTTP](/docs/clients/http/).

Next: [Add a kill switch](/docs/get-started/kill-switch/)
