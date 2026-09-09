import assert from "node:assert/strict";
import test from "node:test";
import { ErrorCode, OpenFeature } from "@openfeature/server-sdk";
import { createNonaClient } from "nona-client";
import {
  configValueResponse,
  jsonResponse,
} from "../../javascript/test/helpers.mjs";
import { createNonaOpenFeatureProvider } from "../dist/index.js";

test("OpenFeature provider resolves typed values through the Nona client", async () => {
  const calls = [];
  const values = new Map([
    ["enabled", ["true", "boolean"]],
    ["limit", ["42", "number"]],
    ["title", ["Checkout", "text"]],
    ["settings", ['{"color":"green","enabled":true}', "json"]],
  ]);
  const nona = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url, init) => {
      calls.push({ url, init });
      const key = decodeURIComponent(new URL(url).pathname.split("/").at(-1));
      const value = values.get(key);
      assert.ok(value, `Unexpected flag key '${key}'.`);
      return configValueResponse(value[0], value[1]);
    },
  });
  const domain = `nona-js-${Date.now()}`;

  await OpenFeature.setProviderAndWait(
    domain,
    createNonaOpenFeatureProvider(nona),
  );

  const client = OpenFeature.getClient(domain);

  assert.equal(await client.getBooleanValue("enabled", false), true);
  assert.equal(await client.getNumberValue("limit", 0), 42);
  assert.equal(await client.getStringValue("title", "fallback"), "Checkout");
  assert.deepEqual(await client.getObjectValue("settings", {}), {
    color: "green",
    enabled: true,
  });
  assert.equal(calls.length, 4);
  assert.equal(
    calls[0].init.headers.get("X-Api-Key"),
    "api-key",
  );
  assert.equal(new URL(calls[0].url).pathname, "/api/environments/production/parameters/enabled");
});

test("OpenFeature provider returns defaults and flag-not-found details for missing Nona values", async () => {
  const provider = createNonaOpenFeatureProvider({
    baseUrl: "https://nona.test",
    apiKey: "api-key",
    environmentId: "production",
    fetch: async () =>
      jsonResponse({
        title: "Not Found",
        status: 404,
        detail: "Config entry not found",
        errorCode: "config_entry_not_found",
      }, 404),
  });
  const domain = `nona-js-missing-${Date.now()}`;

  await OpenFeature.setProviderAndWait(domain, provider);

  const details = await OpenFeature.getClient(domain).getBooleanDetails(
    "missing",
    true,
  );

  assert.equal(details.value, true);
  assert.equal(details.errorCode, ErrorCode.FLAG_NOT_FOUND);
  assert.equal(details.errorMessage, "Config entry not found");
});

test("OpenFeature provider keeps other HTTP failures as provider errors", async () => {
  const provider = createNonaOpenFeatureProvider({
    baseUrl: "https://nona.test",
    apiKey: "api-key",
    environmentId: "production",
    fetch: async () => jsonResponse({
      title: "Not Found",
      status: 404,
      detail: "Environment not found",
      errorCode: "environment_not_found",
    }, 404),
  });
  const domain = `nona-js-provider-error-${Date.now()}`;
  await OpenFeature.setProviderAndWait(domain, provider);

  const details = await OpenFeature.getClient(domain).getBooleanDetails("missing", true);

  assert.equal(details.value, true);
  assert.equal(details.errorCode, ErrorCode.GENERAL);
});

test("OpenFeature provider passes release selection through nona-client", async () => {
  const calls = [];
  const provider = createNonaOpenFeatureProvider({
    baseUrl: "https://nona.test",
    apiKey: "api-key",
    environmentId: "production",
    useReleases: true,
    releaseVersion: "2.1.x",
    fetch: async (url, init) => {
      calls.push({ url, init });
      return configValueResponse("true", "boolean");
    },
  });
  const domain = `nona-js-release-${Date.now()}`;
  await OpenFeature.setProviderAndWait(domain, provider);

  assert.equal(await OpenFeature.getClient(domain).getBooleanValue("enabled", false), true);
  assert.equal(
    calls[0].url,
    "https://nona.test/api/environments/production/releases/2.1.x/parameters/enabled",
  );
});
