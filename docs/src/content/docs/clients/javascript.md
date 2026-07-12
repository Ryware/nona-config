---
title: JavaScript client
description: Read Nona config values from JavaScript or TypeScript.
---

Package: `nona-client`

OpenFeature provider package: `nona-openfeature-provider`

Requirements:

- Node.js 18 or newer, or a runtime with `fetch`, `Headers`, and `Response`
- ESM imports

The JavaScript client is a good fit for:

- Node.js services
- server-side JavaScript applications
- React Native and similar environments that can use `fetch`
- teams that want a lighter integration than OpenFeature but more convenience than raw HTTP

## Install

```bash
npm install nona-client
```

## Read a string

```js
import { createNonaClient } from "nona-client";

const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  environmentId: "production",
  apiKey: process.env.NONA_API_KEY
});

const checkout = await nona.getStringValue("Features:Checkout");

const checkoutEnabled = checkout === "true";
```

For actual feature flags, it is usually better to keep the entry typed as `boolean`, then inspect the metadata or use OpenFeature if you want a flag-oriented interface.

## Read value metadata

```js
const value = await nona.getConfigValue("Features:Checkout");

console.log(value.value);
console.log(value.contentType);
```

`contentType` is one of `text`, `number`, `boolean`, or `json`.

This is useful when one application needs to inspect the logical type before deciding how to handle the value.

## Read JSON

```js
const settings = await nona.getJsonValue("App:Settings");
```

Use JSON when related settings belong together and your application naturally consumes them as one object.

## Return `null` for missing keys

```js
const value = await nona.tryGetConfigValue("Missing:Key");

if (value === null) {
  console.log("Key was not found");
}
```

This is helpful for optional settings or cases where a key may not exist in every environment yet.

## Handle HTTP errors

```js
import { createNonaClient, NonaClientError } from "nona-client";

const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  environmentId: "production",
  apiKey: process.env.NONA_API_KEY
});

try {
  await nona.getConfigValue("Missing:Key");
} catch (error) {
  if (error instanceof NonaClientError) {
    console.error(error.status);
    console.error(error.message);
    throw error;
  }

  throw error;
}
```

## When to use the JavaScript client

Use the JavaScript client when you want:

- a straightforward Nona-specific API
- runtime reads in JavaScript or TypeScript
- optional in-memory caching
- a smaller abstraction layer than OpenFeature

Use [HTTP](/docs/clients/http/) instead when the app only needs one very small direct read path.

## Optional cache

```js
const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  environmentId: "production",
  apiKey: process.env.NONA_API_KEY,
  cacheTtlMs: 30_000,
  cacheMemoryLimitMegabytes: 5
});
```

Use `invalidateTtlCache(key)` to remove one cached value or `clearTtlCache()` to clear all cached values.

Cache is useful when:

- the same keys are read repeatedly
- you want to reduce request volume
- the application can tolerate slightly older values for a short TTL

Keep the TTL short for operational flags and kill switches unless you are sure longer cache windows are acceptable.

## OpenFeature provider

Install the optional provider package alongside the Nona client and OpenFeature server SDK:

```bash
npm install nona-client nona-openfeature-provider @openfeature/server-sdk
```

```js
import { OpenFeature } from "@openfeature/server-sdk";
import { createNonaOpenFeatureProvider } from "nona-openfeature-provider";

const domain = "nona-production";

await OpenFeature.setProviderAndWait(domain, createNonaOpenFeatureProvider({
  baseUrl: "https://nona.example.com",
  apiKey: process.env.NONA_API_KEY,
  environmentId: "production"
}));

const client = OpenFeature.getClient(domain);
const enabled = await client.getBooleanValue("Features:Checkout", false);
```

If your team thinks in terms of feature flags more than direct config reads, see [OpenFeature](/docs/clients/openfeature/).
