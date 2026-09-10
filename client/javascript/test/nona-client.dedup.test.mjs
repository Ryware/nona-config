import assert from "node:assert/strict";
import test from "node:test";
import { createNonaClient } from "../dist/index.js";
import { configValueResponse, deferred, isNonaError, jsonResponse } from "./helpers.mjs";

test("three concurrent identical requests deduplicate to one HTTP call", async () => {
  const pending = deferred();
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async () => {
      calls += 1;
      return pending.promise;
    }
  });

  const a = client.getConfigValue("homepage");
  const b = client.getConfigValue("homepage");
  const c = client.getConfigValue("homepage");

  assert.equal(calls, 1);

  pending.resolve(configValueResponse("v1", "text"));

  const [ra, rb, rc] = await Promise.all([a, b, c]);
  assert.equal(ra.value, "v1");
  assert.equal(rb.value, "v1");
  assert.equal(rc.value, "v1");
});

test("concurrent bulk requests deduplicate case-insensitive prefixes", async () => {
  const pending = deferred();
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async () => {
      calls += 1;
      return pending.promise;
    }
  });

  const first = client.getAllValues({ prefix: "GroupA:" });
  const second = client.getAllValues({ prefix: "groupa:" });

  assert.equal(calls, 1);
  pending.resolve(jsonResponse({
    "GroupA:One": { value: "1", contentType: "number" }
  }));

  assert.deepEqual(await first, await second);
});

test("concurrent bulk requests keep Unicode prefixes distinct from ASCII prefixes", async () => {
  const calls = [];
  const validationMessage =
    "Prefix may contain only ASCII letters, digits, colons, dots, underscores, and dashes.";
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url) => {
      calls.push(String(url));
      const prefix = new URL(url).searchParams.get("prefix");
      if (prefix === "ß") {
        return jsonResponse({
          title: "Bad Request",
          status: 400,
          detail: validationMessage
        }, 400);
      }

      return jsonResponse({
        "SS:Flag": { value: "true", contentType: "boolean" }
      });
    }
  });

  const invalid = client.getAllValues({ prefix: "ß" });
  const valid = client.getAllValues({ prefix: "SS" });

  await assert.rejects(invalid, isNonaError(400, validationMessage));
  assert.deepEqual(await valid, {
    "SS:Flag": { value: "true", contentType: "boolean" }
  });
  assert.equal(calls.length, 2);
  assert.match(calls[0], /prefix=%C3%9F$/);
  assert.match(calls[1], /prefix=SS$/);

  await assert.rejects(
    () => client.getAllValues({ prefix: "ß" }),
    isNonaError(400, validationMessage)
  );
  assert.equal(calls.length, 3);
});

test("three concurrent different requests result in three HTTP calls", async () => {
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url) => {
      calls += 1;
      return configValueResponse(String(url), "text");
    }
  });

  await Promise.all([
    client.getConfigValue("homepage"),
    client.getConfigValue("footer"),
    client.getConfigValue("sidebar")
  ]);

  assert.equal(calls, 3);
});

test("failed in-flight request propagates same error to all callers", async () => {
  const pending = deferred();
  let calls = 0;
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async () => {
      calls += 1;
      return pending.promise;
    }
  });

  const a = client.getConfigValue("homepage");
  const b = client.getConfigValue("homepage");
  const c = client.getConfigValue("homepage");

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
    environmentId: "production",
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
    () => client.getConfigValue("homepage"),
    isNonaError(500, "boom")
  );

  const second = await client.getConfigValue("homepage");
  assert.equal(second.value, "ok");
  assert.equal(calls, 2);
});
