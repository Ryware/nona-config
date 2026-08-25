import assert from "node:assert/strict";
import test from "node:test";
import {
  createNonaClient,
  NonaClientError,
  NonaProjectRoles,
  NonaUserRoles
} from "../dist/index.js";
import { capture, configValueResponse, jsonResponse } from "./helpers.mjs";

test("exports organization and project roles separately", () => {
  assert.deepEqual(NonaUserRoles, { Admin: "admin", Member: "member" });
  assert.deepEqual(NonaProjectRoles, { Viewer: "viewer", Editor: "editor" });
});

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

test("failed requests read Problem Details messages", async () => {
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async () => jsonResponse({
      type: "https://tools.ietf.org/html/rfc9110#section-15.5.5",
      title: "Not Found",
      status: 404,
      detail: "Config entry not found",
      instance: "/api/production/missing"
    }, 404)
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

test("getAllValues fetches all values once and primes six local reads", async () => {
  const calls = [];
  const flags = Object.fromEntries(
    Array.from({ length: 6 }, (_, index) => [
      `flag-${index + 1}`,
      { value: index % 2 === 0 ? "true" : "false", contentType: "boolean" }
    ])
  );
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      return new Response(JSON.stringify(flags), {
        status: 200,
        headers: { "Content-Type": "application/json", ETag: '"release-1"' }
      });
    }
  });

  const values = await client.getAllValues();
  const reads = await Promise.all(
    Object.keys(flags).map((key) => client.tryGetConfigValue(key))
  );

  assert.deepEqual(values, flags);
  assert.deepEqual(reads, Object.values(flags));
  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, "https://nona.test/api/production");
});

test("getAllValues uses ETag validation and reuses the snapshot on 304", async () => {
  const calls = [];
  const flags = { banner: { value: "hello", contentType: "text" } };
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      if (calls.length === 1) {
        return new Response(JSON.stringify(flags), {
          status: 200,
          headers: { "Content-Type": "application/json", ETag: '"snapshot"' }
        });
      }

      return new Response(null, {
        status: 304,
        headers: { ETag: '"snapshot"' }
      });
    }
  });

  const first = await client.getAllValues();
  const second = await client.getAllValues();

  assert.deepEqual(second, first);
  assert.equal(calls.length, 2);
  assert.equal(calls[1].headers.get("If-None-Match"), '"snapshot"');
});

test("getAllValues isolates prefix snapshots and shares case-insensitive ETags", async () => {
  const calls = [];
  const groupA = { "GroupA:One": { value: "1", contentType: "number" } };
  const groupB = { "GroupB:One": { value: "2", contentType: "number" } };
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url, init) => {
      const call = capture(url, init);
      calls.push(call);
      const prefix = new URL(url).searchParams.get("prefix");
      if (prefix?.toUpperCase() === "GROUPA:") {
        if (call.headers.get("If-None-Match") === '"group-a"') {
          return new Response(null, { status: 304, headers: { ETag: '"group-a"' } });
        }

        return new Response(JSON.stringify(groupA), {
          status: 200,
          headers: { "Content-Type": "application/json", ETag: '"group-a"' }
        });
      }

      return new Response(JSON.stringify(prefix ? groupB : { ...groupA, ...groupB }), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
          ETag: prefix ? '"group-b"' : '"all"'
        }
      });
    }
  });

  const first = await client.getAllValues({ prefix: "GroupA:" });
  const samePrefixDifferentCase = await client.getAllValues({ prefix: "groupa:" });
  const otherPrefix = await client.getAllValues({ prefix: "GroupB:" });
  const unfiltered = await client.getAllValues({ prefix: "" });

  assert.deepEqual(first, groupA);
  assert.deepEqual(samePrefixDifferentCase, groupA);
  assert.deepEqual(otherPrefix, groupB);
  assert.deepEqual(unfiltered, { ...groupA, ...groupB });
  assert.equal(calls[0].url, "https://nona.test/api/production?prefix=GroupA%3A");
  assert.equal(calls[1].url, "https://nona.test/api/production?prefix=groupa%3A");
  assert.equal(calls[1].headers.get("If-None-Match"), '"group-a"');
  assert.equal(calls[2].headers.get("If-None-Match"), null);
  assert.equal(calls[3].url, "https://nona.test/api/production");
});

test("prefixed bulk reads prime only returned single-key values", async () => {
  const calls = [];
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      if (new URL(url).searchParams.has("prefix")) {
        return new Response(JSON.stringify({
          "GroupA:One": { value: "1", contentType: "number" }
        }), {
          status: 200,
          headers: { "Content-Type": "application/json", ETag: '"group-a"' }
        });
      }

      return configValueResponse("2", "number");
    }
  });

  await client.getAllValues({ prefix: "GroupA:" });
  assert.equal((await client.getConfigValue("GroupA:One")).value, "1");
  assert.equal((await client.getConfigValue("GroupB:One")).value, "2");
  assert.equal(calls.length, 2);
});

test("getAllValues isolates cached values from caller mutation", async () => {
  const calls = [];
  const flags = { banner: { value: "hello", contentType: "text" } };
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      if (calls.length === 1) {
        return new Response(JSON.stringify(flags), {
          status: 200,
          headers: { "Content-Type": "application/json", ETag: '"snapshot"' }
        });
      }

      return new Response(null, {
        status: 304,
        headers: { ETag: '"snapshot"' }
      });
    }
  });

  const first = await client.getAllValues();
  first.banner.value = "caller-mutated";
  assert.deepEqual(
    await client.getConfigValue("banner"),
    { value: "hello", contentType: "text" }
  );

  const second = await client.getAllValues();
  second.banner.value = "mutated-again";
  assert.deepEqual(
    await client.getConfigValue("banner"),
    { value: "hello", contentType: "text" }
  );
  assert.equal(calls.length, 2);
});

test("getAllValues re-primes an invalidated value after 304 validation", async () => {
  const calls = [];
  const flags = { banner: { value: "hello", contentType: "text" } };
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      if (calls.length === 1) {
        return new Response(JSON.stringify(flags), {
          status: 200,
          headers: { "Content-Type": "application/json", ETag: '"snapshot"' }
        });
      }

      return new Response(null, {
        status: 304,
        headers: { ETag: '"snapshot"' }
      });
    }
  });

  await client.getAllValues();
  assert.equal(client.invalidateTtlCache("banner"), true);
  await client.getAllValues();
  assert.deepEqual(
    await client.getConfigValue("banner"),
    { value: "hello", contentType: "text" }
  );
  assert.equal(calls.length, 2);
});

test("bulk snapshots share the configured memory limit and evict least-recently-used data", async () => {
  const calls = [];
  const flags = {
    banner: { value: "x".repeat(200), contentType: "text" }
  };
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    cacheMemoryLimitMegabytes: 0.0008,
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      const version = new URL(url).searchParams.get("version");
      if (calls.at(-1).headers.get("If-None-Match")) {
        return new Response(null, {
          status: 304,
          headers: { ETag: `"${version}"` }
        });
      }

      return new Response(JSON.stringify(flags), {
        status: 200,
        headers: { "Content-Type": "application/json", ETag: `"${version}"` }
      });
    }
  });

  await client.getAllValues({ releaseVersion: "1.0.0" });
  await client.getAllValues({ releaseVersion: "2.0.0" });
  await client.getAllValues({ releaseVersion: "2.0.0" });
  await client.getAllValues({ releaseVersion: "1.0.0" });

  assert.equal(calls.length, 4);
  assert.equal(calls[2].headers.get("If-None-Match"), '"2.0.0"');
  assert.equal(calls[3].headers.get("If-None-Match"), null);
});

test("getAllValues supports a release selector", async () => {
  const calls = [];
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    releaseVersion: "1.1.x",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      return jsonResponse({});
    }
  });

  await client.getAllValues();

  assert.equal(calls[0].url, "https://nona.test/api/production?version=1.1.x");
});

test("getAllValues combines release and prefix selectors", async () => {
  const calls = [];
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "api-key",
    fetch: async (url, init) => {
      calls.push(capture(url, init));
      return jsonResponse({});
    }
  });

  await client.getAllValues({ releaseVersion: "1.2.3", prefix: "GroupA:" });

  assert.equal(
    calls[0].url,
    "https://nona.test/api/production?version=1.2.3&prefix=GroupA%3A"
  );
});
