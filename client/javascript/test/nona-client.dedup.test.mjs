import assert from "node:assert/strict";
import test from "node:test";
import { createNonaClient } from "../dist/index.js";
import { configValueResponse, deferred, isNonaError, jsonResponse } from "./helpers.mjs";

test("three concurrent identical requests deduplicate to one HTTP call", async () => {
  const pending = deferred();
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    apiKey: "api-key",
    fetch: async () => {
      calls += 1;
      return pending.promise;
    }
  });

  const a = client.getConfigValue("production", "homepage");
  const b = client.getConfigValue("production", "homepage");
  const c = client.getConfigValue("production", "homepage");

  assert.equal(calls, 1);

  pending.resolve(configValueResponse("v1", "text"));

  const [ra, rb, rc] = await Promise.all([a, b, c]);
  assert.equal(ra.value, "v1");
  assert.equal(rb.value, "v1");
  assert.equal(rc.value, "v1");
});

test("three concurrent different requests result in three HTTP calls", async () => {
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    apiKey: "api-key",
    fetch: async (url) => {
      calls += 1;
      return configValueResponse(String(url), "text");
    }
  });

  await Promise.all([
    client.getConfigValue("production", "homepage"),
    client.getConfigValue("production", "footer"),
    client.getConfigValue("staging", "homepage")
  ]);

  assert.equal(calls, 3);
});

test("failed in-flight request propagates same error to all callers", async () => {
  const pending = deferred();
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    apiKey: "api-key",
    fetch: async () => {
      calls += 1;
      return pending.promise;
    }
  });

  const a = client.getConfigValue("production", "homepage");
  const b = client.getConfigValue("production", "homepage");
  const c = client.getConfigValue("production", "homepage");

  assert.equal(calls, 1);

  pending.resolve(jsonResponse({ error: "boom" }, 500));

  await Promise.all([
    assert.rejects(a, isNonaError(500, "boom")),
    assert.rejects(b, isNonaError(500, "boom")),
    assert.rejects(c, isNonaError(500, "boom"))
  ]);
});

test("failed request cleanup allows subsequent retries", async () => {
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    apiKey: "api-key",
    fetch: async () => {
      calls += 1;
      if (calls === 1) {
        return jsonResponse({ error: "boom" }, 500);
      }

      return configValueResponse("ok", "text");
    }
  });

  await assert.rejects(
    () => client.getConfigValue("production", "homepage"),
    isNonaError(500, "boom")
  );

  const second = await client.getConfigValue("production", "homepage");
  assert.equal(second.value, "ok");
  assert.equal(calls, 2);
});
