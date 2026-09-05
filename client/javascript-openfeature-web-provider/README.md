# nona-openfeature-web-provider

OpenFeature **web** provider for **Nona**. Use Nona remote config and feature flags in the browser through the OpenFeature web SDK.

For Node.js and other backends, use [`nona-openfeature-provider`](../javascript-openfeature-provider) instead. The two packages exist because OpenFeature has two paradigms: the server SDK evaluates per request with a dynamic context, while the web SDK evaluates synchronously against a static context.

## Install

```bash
npm install nona-client nona-openfeature-web-provider @openfeature/web-sdk
```

## Usage

```js
import { OpenFeature } from "@openfeature/web-sdk";
import { createNonaOpenFeatureWebProvider } from "nona-openfeature-web-provider";

await OpenFeature.setProviderAndWait(
  createNonaOpenFeatureWebProvider({
    baseUrl: "https://nona.example.com",
    apiKey: "your-frontend-api-key",
    environmentId: "production"
  })
);

const client = OpenFeature.getClient();

// Synchronous — the snapshot is already in memory.
const enabled = client.getBooleanValue("Features:Checkout", false);
```

You can also hand it an existing Nona client:

```js
import { createNonaClient } from "nona-client";

const provider = createNonaOpenFeatureWebProvider(createNonaClient({ ... }), {
  prefix: "Features:"
});
```

## Requires a frontend-scoped API key

The provider loads the whole environment as one snapshot, so it uses Nona's client-facing snapshot endpoint (`GET /api/{environmentId}`). That endpoint:

- requires an API key with the **frontend** scope, and
- returns only **frontend-scoped** config entries.

A backend-only key gets a `404` rather than a `403`, so that a server key cannot be used to enumerate environments. The provider turns both `401` and `404` into a fatal initialization error naming the likely cause, since neither is worth retrying.

Mark the entries you want browsers to see as frontend-scoped in Nona, and make sure the Nona server allows your site's origin via CORS.

## Options

| Option | Default | Description |
| --- | --- | --- |
| `prefix` | none | Restrict the snapshot to keys under this prefix, e.g. `Features:`. |
| `pollIntervalMs` | `30000` | How often to poll for changes. Pass `0` to disable and call `refresh()` yourself. |
| `metadataName` | `nona` | Name reported as the OpenFeature provider metadata name. |
| `logger` | none | Receives a message when a background refresh fails. |

Everything `createNonaClient` accepts (`baseUrl`, `apiKey`, `environmentId`, `releaseVersion`, `fetch`, …) is accepted here too when you pass options rather than a client.

## Staying up to date

Polling sends the snapshot's `ETag`, so an unchanged environment costs a `304` and no re-render. When values do change, the provider emits `PROVIDER_CONFIGURATION_CHANGED` with the changed keys, which is what drives re-evaluation in the web SDK and re-renders in the React SDK.

To refresh at a moment of your choosing — after a login, or on `visibilitychange` — call `refresh()`. It returns the keys that changed:

```js
const changed = await provider.refresh();
```

## Evaluation context

Nona snapshots are the same for every caller today, so there is no targeting: `onContextChange` refetches the snapshot and the evaluation context is otherwise unused. Setting a context is safe and forward-compatible; it just does not change which values you get yet.

## Error codes

| Situation | Error code |
| --- | --- |
| Evaluated before `initialize`, or after `close` | `PROVIDER_NOT_READY` |
| Key absent from the snapshot (including entries that are not frontend-scoped) | `FLAG_NOT_FOUND` |
| Value is not a valid boolean or number | `TYPE_MISMATCH` |
| Value is not valid JSON | `PARSE_ERROR` |

## Test

```bash
npm test
```
