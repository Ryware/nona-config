import assert from "node:assert/strict";
import test from "node:test";
import { createNonaClient } from "../dist/index.js";
import { configValueResponse, wait } from "./helpers.mjs";

test("ttl cache is disabled by default", async () => {
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    apiKey: "api-key",
    fetch: async () => {
      calls += 1;
      return configValueResponse("v1", "text");
    }
  });

  await client.getConfigValue("production", "homepage");
  await client.getConfigValue("production", "homepage");

  assert.equal(calls, 2);
});

test("ttl cache hit returns from memory when enabled", async () => {
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    apiKey: "api-key",
    cacheTtlMs: 5000,
    fetch: async () => {
      calls += 1;
      return configValueResponse("v1", "text");
    }
  });

  await client.getConfigValue("production", "homepage");
  await client.getConfigValue("production", "homepage");

  assert.equal(calls, 1);
});

test("expired ttl cache performs a new network request", async () => {
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    apiKey: "api-key",
    cacheTtlMs: 5,
    fetch: async () => {
      calls += 1;
      return configValueResponse(`v${calls}`, "text");
    }
  });

  const first = await client.getConfigValue("production", "homepage");
  await wait(15);
  const second = await client.getConfigValue("production", "homepage");

  assert.equal(first.value, "v1");
  assert.equal(second.value, "v2");
  assert.equal(calls, 2);
});

test("targeted cache invalidation removes only matching entry", async () => {
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    apiKey: "api-key",
    cacheTtlMs: 5000,
    fetch: async (url) => {
      calls += 1;
      return configValueResponse(String(url), "text");
    }
  });

  await client.getConfigValue("production", "homepage");
  await client.getConfigValue("production", "footer");
  assert.equal(calls, 2);

  assert.equal(client.invalidateTtlCache("production", "homepage"), true);
  assert.equal(client.invalidateTtlCache("production", "missing"), false);

  await client.getConfigValue("production", "homepage");
  await client.getConfigValue("production", "footer");

  assert.equal(calls, 3);
});

test("clear cache removes all ttl entries", async () => {
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    apiKey: "api-key",
    cacheTtlMs: 5000,
    fetch: async (url) => {
      calls += 1;
      return configValueResponse(String(url), "text");
    }
  });

  await client.getConfigValue("production", "homepage");
  await client.getConfigValue("production", "footer");
  assert.equal(calls, 2);

  client.clearTtlCache();

  await client.getConfigValue("production", "homepage");
  await client.getConfigValue("production", "footer");
  assert.equal(calls, 4);
});
