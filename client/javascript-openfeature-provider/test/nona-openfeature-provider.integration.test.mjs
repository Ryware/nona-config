import assert from "node:assert/strict";
import test from "node:test";
import { ErrorCode, OpenFeature } from "@openfeature/server-sdk";
import { createNonaOpenFeatureProvider } from "../dist/index.js";

const settings = getIntegrationSettings();

test(
  "OpenFeature provider resolves values from a real Nona config server",
  { skip: settings ? false : "Set NONA_INTEGRATION_BASE_URL, NONA_INTEGRATION_API_KEY, and NONA_INTEGRATION_ENVIRONMENT_ID to run." },
  async () => {
    const domain = `nona-js-integration-${Date.now()}`;

    await OpenFeature.setProviderAndWait(
      domain,
      createNonaOpenFeatureProvider({
        baseUrl: settings.baseUrl,
        apiKey: settings.apiKey,
        environmentId: settings.environmentId,
      }),
    );

    const client = OpenFeature.getClient(domain);

    assert.equal(
      await client.getBooleanValue(settings.booleanKey, !settings.booleanValue),
      settings.booleanValue,
    );
    assert.equal(
      await client.getNumberValue(settings.numberKey, settings.numberValue + 1),
      settings.numberValue,
    );
    assert.equal(
      await client.getStringValue(settings.stringKey, "fallback"),
      settings.stringValue,
    );
    assert.deepEqual(
      await client.getObjectValue(settings.objectKey, {}),
      JSON.parse(settings.objectValue),
    );

    const missing = await client.getBooleanDetails(
      `missing-${Date.now()}`,
      true,
    );
    assert.equal(missing.value, true);
    assert.equal(missing.errorCode, ErrorCode.FLAG_NOT_FOUND);
  },
);

function getIntegrationSettings() {
  const baseUrl = process.env.NONA_INTEGRATION_BASE_URL;
  const apiKey = process.env.NONA_INTEGRATION_API_KEY;
  const environmentId = process.env.NONA_INTEGRATION_ENVIRONMENT_ID;

  if (!baseUrl || !apiKey || !environmentId) {
    return null;
  }

  return {
    baseUrl,
    apiKey,
    environmentId,
    booleanKey: process.env.NONA_INTEGRATION_BOOLEAN_KEY ?? "openfeature:boolean",
    booleanValue: parseBoolean(
      process.env.NONA_INTEGRATION_BOOLEAN_VALUE ?? "true",
    ),
    numberKey: process.env.NONA_INTEGRATION_NUMBER_KEY ?? "openfeature:number",
    numberValue: Number(process.env.NONA_INTEGRATION_NUMBER_VALUE ?? "42"),
    stringKey: process.env.NONA_INTEGRATION_STRING_KEY ?? "openfeature:string",
    stringValue: process.env.NONA_INTEGRATION_STRING_VALUE ?? "Checkout",
    objectKey: process.env.NONA_INTEGRATION_OBJECT_KEY ?? "openfeature:object",
    objectValue:
      process.env.NONA_INTEGRATION_OBJECT_VALUE ??
      '{"color":"green","enabled":true}',
  };
}

function parseBoolean(value) {
  if (value.toLowerCase() === "true") {
    return true;
  }

  if (value.toLowerCase() === "false") {
    return false;
  }

  throw new Error(`Invalid boolean integration value '${value}'.`);
}
