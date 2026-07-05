# nona-openfeature-provider

OpenFeature provider for **Nona**. Use Nona remote config and feature flags through the OpenFeature server SDK.

## Install

```bash
npm install nona-client nona-openfeature-provider @openfeature/server-sdk
```

## Usage

```js
import { OpenFeature } from "@openfeature/server-sdk";
import { createNonaOpenFeatureProvider } from "nona-openfeature-provider";

await OpenFeature.setProviderAndWait(
  createNonaOpenFeatureProvider({
    baseUrl: "https://nona.example.com",
    apiKey: "your-api-key",
    environmentId: "production"
  })
);

const client = OpenFeature.getClient();
const enabled = await client.getBooleanValue("Features:Checkout", false);
```

Nona API keys are bound to a project, so provider configuration only needs the Nona server URL, API key, and environment id.

## Integration test

The test suite includes an optional real-server test. Seed a Nona environment with these entries, or override the key/value environment variables:

- `openfeature:boolean` = `true` (`boolean`)
- `openfeature:number` = `42` (`number`)
- `openfeature:string` = `Checkout` (`text`)
- `openfeature:object` = `{"color":"green","enabled":true}` (`json`)

```bash
NONA_INTEGRATION_BASE_URL=http://localhost:18080 \
NONA_INTEGRATION_API_KEY=your-api-key \
NONA_INTEGRATION_ENVIRONMENT_ID=production \
npm test
```
