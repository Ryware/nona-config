# nona-client

Official JavaScript/Node.js client for **Nona** — an open-source, self-hosted remote configuration and feature flag service, and a Firebase Remote Config alternative you run yourself. Read your config values and feature flags at runtime with a single typed call.

- Website: https://nonaconfig.com
- Source & docs: https://github.com/Ryware/nona-config/tree/development/client/javascript

## Install

```bash
npm install nona-client
```

## Import

This package is ESM-only, so use `import`:

```js
import { createNonaClient, NonaClientError } from "nona-client";
```

## Create a client

```js
const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  environmentId: "production",
  apiKey: "your-api-key"
});
```

Reads use the environment's active release by default. To pin a client to an exact release or release line:

```js
const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  environmentId: "production",
  apiKey: "your-api-key",
  releaseVersion: "1.1.x"
});
```

You can also pass the base URL as the first argument:

```js
const nona = createNonaClient("https://nona.example.com", {
  environmentId: "production",
  apiKey: "your-api-key"
});
```

## Read config values

API keys are bound to one project, and the client is bound to one environment, so config reads only take a key.

```js
const value = await nona.getConfigValue("Features:Checkout");
console.log(value.value);
console.log(value.contentType);
```

If you only want the string value:

```js
const checkoutEnabled = await nona.getStringValue("Features:Checkout");
```

If the value contains JSON:

```js
const settings = await nona.getJsonValue("App:Settings");
console.log(settings);
```

If a key might not exist, use `tryGetConfigValue`:

```js
const maybeValue = await nona.tryGetConfigValue("Missing:Key");

if (maybeValue === null) {
  console.log("No value found");
}
```

## Fetch all values at startup

Use one bulk request to fetch every client-visible value and prime subsequent reads:

```js
const values = await nona.getAllValues();

const checkout = await nona.tryGetConfigValue("Features:Checkout");
const banner = await nona.tryGetConfigValue("App:Banner");
```

`values` is a map of `{ key: { value, contentType } }`. The reads after `getAllValues()` are served from the in-memory snapshot even when `cacheTtlMs` is not enabled, so six startup flags require one HTTP request.

The bulk endpoint accepts `client` and `all` API keys. It includes client-visible (`client` and `all`) entries and never returns server-only entries.

Repeated `getAllValues()` calls automatically use the response ETag. An unchanged snapshot produces `304 Not Modified` and reuses the existing values.

## Handle errors

Requests that fail with an HTTP error throw `NonaClientError`:

```js
try {
  await nona.getConfigValue("Missing:Key");
} catch (error) {
  if (error instanceof NonaClientError) {
    console.error(error.status);
    console.error(error.message);
    console.error(error.responseBody);
    return;
  }

  throw error;
}
```

## Options

`createNonaClient` accepts these options:

- `baseUrl`: the Nona server URL
- `environmentId`: environment used for config reads
- `apiKey`: API key for config reads
- `releaseVersion`: optional exact release such as `1.1.0` or line such as `1.1.x`
- `fetch`: custom fetch implementation
- `defaultHeaders`: headers added to every request
- `cacheTtlMs`: cache TTL in milliseconds (disabled by default; set a positive value to enable)
- `cacheMemoryLimitMegabytes`: shared TTL and bulk-snapshot cache limit in MB (default `5`)

Cache helpers:

- `invalidateTtlCache(key, options?)`: removes only the matching cached request
- `clearTtlCache()`: removes all TTL and bulk-primed cache entries

## Runtime requirements

- Node.js 18 or newer
- Or any environment that provides `fetch`, `Headers`, and `Response`
