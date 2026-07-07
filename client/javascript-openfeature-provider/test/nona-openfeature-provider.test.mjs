import assert from "node:assert/strict";
import test from "node:test";
import { ErrorCode, OpenFeature } from "@openfeature/server-sdk";
import { createNonaClient } from "nona-client";
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
  assert.equal(new URL(calls[0].url).pathname, "/api/production/enabled");
});

test("OpenFeature provider returns defaults and flag-not-found details for missing Nona values", async () => {
  const provider = createNonaOpenFeatureProvider({
    baseUrl: "https://nona.test",
    apiKey: "api-key",
    environmentId: "production",
    fetch: async () =>
      jsonResponse({ error: "Config entry not found" }, 404),
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

function jsonResponse(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
    },
  });
}

function configValueResponse(value, contentType = "text", status = 200) {
  return new Response(value, {
    status,
    headers: {
      "Content-Type": "application/json",
      "X-Nona-Content-Type": contentType,
    },
  });
}
