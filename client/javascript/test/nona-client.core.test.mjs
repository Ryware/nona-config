import assert from "node:assert/strict";
import test from "node:test";
import { createNonaClient, NonaClientError } from "../dist/index.js";
import { capture, configValueResponse, jsonResponse } from "./helpers.mjs";

test("getConfigValue sends API key and parses the value", async () => {
  const calls = [];
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      return configValueResponse("enabled", "text");
    }
  });

  const value = await client.getConfigValue("Features:Checkout");

  assert.equal(value.value, "enabled");
  assert.equal(value.contentType, "text");
  assert.equal(calls[0].url, "https://nona.test/api/production/Features%3ACheckout");
  assert.equal(calls[0].headers.get("X-Api-Key"), "api-key");
});

test("getConfigValue sends configured release version", async () => {
  const calls = [];
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    releaseVersion: "1.1.x",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      return configValueResponse("enabled", "text");
    }
  });

  await client.getConfigValue("Features:Checkout");

  assert.equal(
    calls[0].url,
    "https://nona.test/api/production/Features%3ACheckout?version=1.1.x"
  );
});

test("getConfigValue request release version overrides client default", async () => {
  const calls = [];
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    releaseVersion: "1.1.x",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      return configValueResponse("enabled", "text");
    }
  });

  await client.getConfigValue("Features:Checkout", { releaseVersion: "1.1.0" });

  assert.equal(
    calls[0].url,
    "https://nona.test/api/production/Features%3ACheckout?version=1.1.0"
  );
});

test("getConfigValue accepts legacy JSON responses", async () => {
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async () => jsonResponse({ value: "enabled", contentType: "text" })
  });

  const value = await client.getConfigValue("Features:Checkout");

  assert.equal(value.value, "enabled");
  assert.equal(value.contentType, "text");
});

test("getConfigValue allows empty raw values", async () => {
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async () => configValueResponse("", "text")
  });

  const value = await client.getConfigValue("Empty");

  assert.equal(value.value, "");
  assert.equal(value.contentType, "text");
});

test("failed requests throw NonaClientError with backend error message", async () => {
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async () => jsonResponse({ error: "Config entry not found" }, 404)
  });

  await assert.rejects(
    () => client.getConfigValue("missing"),
    error => {
      assert.ok(error instanceof NonaClientError);
      assert.equal(error.status, 404);
      assert.equal(error.message, "Config entry not found");
      return true;
    }
  );
});

test("missing apiKey throws before request execution", async () => {
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    fetch: async () => configValueResponse("enabled", "text")
  });

  await assert.rejects(
    () => client.getConfigValue("Features:Checkout"),
    (error) => {
      assert.equal(error instanceof Error, true);
      assert.equal(
        error.message,
        "Nona API-key calls require createNonaClient(...).apiKey."
      );
      return true;
    }
  );
});

test("missing environmentId throws before request execution", async () => {
  assert.throws(
    () => createNonaClient("https://nona.test", {
      apiKey: "api-key",
      fetch: async () => configValueResponse("enabled", "text")
    }),
    (error) => {
      assert.equal(error instanceof Error, true);
      assert.equal(error.message, "environmentId cannot be empty.");
      return true;
    }
  );
});
