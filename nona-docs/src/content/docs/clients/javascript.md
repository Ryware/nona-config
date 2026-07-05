---
title: JavaScript client
description: Read Nona config values from JavaScript or TypeScript.
---

Package: `nona-client`

OpenFeature provider package: `nona-openfeature-provider`

Requirements:

- Node.js 18 or newer, or a runtime with `fetch`, `Headers`, and `Response`
- ESM imports

## Install

```bash
npm install nona-client
```

## Read a string

```js
import { createNonaClient } from "nona-client";

const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  apiKey: process.env.NONA_API_KEY
});

const checkout = await nona.getStringValue("production", "Features:Checkout");

const checkoutEnabled = checkout === "true";
```

## Read value metadata

```js
const value = await nona.getConfigValue("production", "Features:Checkout");

console.log(value.value);
console.log(value.contentType);
```

`contentType` is one of `text`, `number`, `boolean`, or `json`.

## Read JSON

```js
const settings = await nona.getJsonValue("production", "App:Settings");
```

## Return `null` for missing keys

```js
const value = await nona.tryGetConfigValue("production", "Missing:Key");

if (value === null) {
  console.log("Key was not found");
}
```

## Handle HTTP errors

```js
import { createNonaClient, NonaClientError } from "nona-client";

const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  apiKey: process.env.NONA_API_KEY
});

try {
  await nona.getConfigValue("production", "Missing:Key");
} catch (error) {
  if (error instanceof NonaClientError) {
    console.error(error.status);
    console.error(error.message);
    throw error;
  }

  throw error;
}
```

## Optional cache

```js
const nona = createNonaClient({
  baseUrl: "https://nona.example.com",
  apiKey: process.env.NONA_API_KEY,
  cacheTtlMs: 30_000,
  cacheMemoryLimitMegabytes: 5
});
```

Use `invalidateTtlCache(environmentId, key)` to remove one cached value or `clearTtlCache()` to clear all cached values.

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
