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

## Example

```bash
curl "https://nona.example.com/api/production/Features%3ACheckout" \
  -H "X-Api-Key: $NONA_API_KEY"
```

For the full endpoint behavior, see [HTTP](/docs/clients/http/).

Next: [Add a kill switch](/docs/get-started/kill-switch/)
