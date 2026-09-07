import assert from "node:assert/strict";
import test from "node:test";
import { ErrorCode, OpenFeature, ProviderEvents } from "@openfeature/web-sdk";
import { createNonaClient } from "nona-client";
import { jsonResponse } from "../../javascript/test/helpers.mjs";
import { createNonaOpenFeatureWebProvider } from "../dist/index.js";

const snapshot = {
  enabled: { value: "true", contentType: "boolean" },
  limit: { value: "42", contentType: "number" },
  title: { value: "Checkout", contentType: "text" },
  settings: { value: '{"color":"green","enabled":true}', contentType: "json" },
};

function snapshotResponse(values, { etag, status = 200 } = {}) {
  return new Response(status === 304 ? null : JSON.stringify(values), {
    status,
    headers: {
      "Content-Type": "application/json",
      ...(etag ? { ETag: etag } : {}),
    },
  });
}

function stubClient(handler, options = {}) {
  const calls = [];
  const client = createNonaClient("https://nona.test", {
    environmentId: "production",
    apiKey: "frontend-key",
    ...options,
    fetch: async (url, init) => {
      calls.push({ url, init });
      return handler(calls.length, { url, init });
    },
  });

  return { client, calls };
}

function domainFor(name) {
  return `nona-web-${name}-${Date.now()}`;
}

test("web provider resolves typed values synchronously from a single snapshot fetch", async () => {
  const { client, calls } = stubClient(() => snapshotResponse(snapshot));
  const provider = createNonaOpenFeatureWebProvider(client, {
    pollIntervalMs: 0,
  });
  const domain = domainFor("typed");

  await OpenFeature.setProviderAndWait(domain, provider);
  const ofClient = OpenFeature.getClient(domain);

  // Synchronous — values, not promises.
  assert.equal(ofClient.getBooleanValue("enabled", false), true);
  assert.equal(ofClient.getNumberValue("limit", 0), 42);
  assert.equal(ofClient.getStringValue("title", "fallback"), "Checkout");
  assert.deepEqual(ofClient.getObjectValue("settings", {}), {
    color: "green",
    enabled: true,
  });

  assert.equal(calls.length, 1, "evaluation must not hit the network");
  assert.equal(new URL(calls[0].url).pathname, "/api/production/parameters");
  assert.equal(calls[0].init.headers.get("X-Api-Key"), "frontend-key");

  const details = ofClient.getBooleanDetails("enabled", false);
  assert.equal(details.reason, "STATIC");
  assert.equal(details.flagMetadata.contentType, "boolean");
  assert.equal(details.flagMetadata.nonaKey, "enabled");

  await provider.onClose();
});

test("web provider sends the snapshot prefix", async () => {
  const { client, calls } = stubClient(() => snapshotResponse(snapshot));
  const provider = createNonaOpenFeatureWebProvider(client, {
    prefix: "Features:",
    pollIntervalMs: 0,
  });

  await provider.initialize();

  assert.equal(new URL(calls[0].url).searchParams.get("prefix"), "Features:");

  await provider.onClose();
});

test("web provider passes release selection through nona-client", async () => {
  const calls = [];
  const provider = createNonaOpenFeatureWebProvider({
    baseUrl: "https://nona.test",
    environmentId: "production",
    apiKey: "frontend-key",
    useReleases: true,
    releaseVersion: "2.1.x",
    pollIntervalMs: 0,
    fetch: async (url, init) => {
      calls.push({ url, init });
      return snapshotResponse(snapshot);
    },
  });

  await provider.initialize();

  assert.equal(
    calls[0].url,
    "https://nona.test/api/production/releases/parameters?version=2.1.x",
  );
  await provider.onClose();
});

test("web provider reports missing, mistyped, and unparsable flags", async () => {
  const { client } = stubClient(() =>
    snapshotResponse({
      notABoolean: { value: "yes", contentType: "text" },
      notANumber: { value: "", contentType: "number" },
      brokenJson: { value: "{not json", contentType: "json" },
    }),
  );
  const provider = createNonaOpenFeatureWebProvider(client, {
    pollIntervalMs: 0,
  });
  const domain = domainFor("errors");

  await OpenFeature.setProviderAndWait(domain, provider);
  const ofClient = OpenFeature.getClient(domain);

  const missing = ofClient.getBooleanDetails("missing", true);
  assert.equal(missing.value, true);
  assert.equal(missing.errorCode, ErrorCode.FLAG_NOT_FOUND);
  assert.match(missing.errorMessage, /frontend-scoped/);

  const mistyped = ofClient.getBooleanDetails("notABoolean", false);
  assert.equal(mistyped.value, false);
  assert.equal(mistyped.errorCode, ErrorCode.TYPE_MISMATCH);

  const notANumber = ofClient.getNumberDetails("notANumber", 7);
  assert.equal(notANumber.value, 7);
  assert.equal(notANumber.errorCode, ErrorCode.TYPE_MISMATCH);

  const broken = ofClient.getObjectDetails("brokenJson", { fallback: true });
  assert.deepEqual(broken.value, { fallback: true });
  assert.equal(broken.errorCode, ErrorCode.PARSE_ERROR);

  await provider.onClose();
});

test("web provider resolves flag keys that collide with object prototype members", async () => {
  const { client } = stubClient(() =>
    snapshotResponse({
      toString: { value: "true", contentType: "boolean" },
    }),
  );
  const provider = createNonaOpenFeatureWebProvider(client, {
    pollIntervalMs: 0,
  });
  const domain = domainFor("prototype");

  await OpenFeature.setProviderAndWait(domain, provider);
  const ofClient = OpenFeature.getClient(domain);

  assert.equal(ofClient.getBooleanValue("toString", false), true);

  const inherited = ofClient.getBooleanDetails("constructor", false);
  assert.equal(inherited.value, false);
  assert.equal(inherited.errorCode, ErrorCode.FLAG_NOT_FOUND);

  await provider.onClose();
});

test("refresh emits a configuration change for the keys that moved", async () => {
  const responses = [
    () => snapshotResponse(snapshot, { etag: 'W/"1"' }),
    () =>
      snapshotResponse(
        {
          ...snapshot,
          enabled: { value: "false", contentType: "boolean" },
          added: { value: "new", contentType: "text" },
          title: undefined,
        },
        { etag: 'W/"2"' },
      ),
  ];
  const { client } = stubClient((call) => {
    const next = responses[call - 1] ?? responses[responses.length - 1];
    return next();
  });
  const provider = createNonaOpenFeatureWebProvider(client, {
    pollIntervalMs: 0,
  });

  const events = [];
  provider.events.addHandler(ProviderEvents.ConfigurationChanged, (details) => {
    events.push(details);
  });

  await provider.initialize();
  const changed = await provider.refresh();

  assert.deepEqual(changed.sort(), ["added", "enabled", "title"]);
  assert.equal(events.length, 1);
  assert.deepEqual([...events[0].flagsChanged].sort(), [
    "added",
    "enabled",
    "title",
  ]);

  await provider.onClose();
});

test("refresh over an unchanged snapshot emits nothing", async () => {
  const { client, calls } = stubClient((call, { init }) => {
    if (call === 1) {
      return snapshotResponse(snapshot, { etag: 'W/"1"' });
    }

    assert.equal(init.headers.get("If-None-Match"), 'W/"1"');
    return snapshotResponse(undefined, { status: 304, etag: 'W/"1"' });
  });
  const provider = createNonaOpenFeatureWebProvider(client, {
    pollIntervalMs: 0,
  });

  const events = [];
  provider.events.addHandler(ProviderEvents.ConfigurationChanged, (details) => {
    events.push(details);
  });

  await provider.initialize();
  const changed = await provider.refresh();

  assert.equal(calls.length, 2);
  assert.deepEqual(changed, []);
  assert.deepEqual(events, []);

  await provider.onClose();
});

test("a backend-only key or unknown environment fails initialization fatally", async () => {
  const provider = createNonaOpenFeatureWebProvider({
    baseUrl: "https://nona.test",
    apiKey: "backend-key",
    environmentId: "production",
    pollIntervalMs: 0,
    fetch: async () => jsonResponse({ error: "Environment not found" }, 404),
  });

  await assert.rejects(() => provider.initialize(), (thrown) => {
    assert.equal(thrown.code, ErrorCode.PROVIDER_FATAL);
    assert.match(thrown.message, /frontend-scoped/);
    return true;
  });

  await provider.onClose();
});

test("a rejected API key fails initialization fatally", async () => {
  const provider = createNonaOpenFeatureWebProvider({
    baseUrl: "https://nona.test",
    apiKey: "nope",
    environmentId: "production",
    pollIntervalMs: 0,
    fetch: async () => jsonResponse({ error: "Invalid API key" }, 401),
  });

  await assert.rejects(() => provider.initialize(), (thrown) => {
    assert.equal(thrown.code, ErrorCode.PROVIDER_FATAL);
    assert.match(thrown.message, /Invalid API key/);
    return true;
  });
});

test("a transient failure stays retryable rather than fatal", async (t) => {
  const provider = createNonaOpenFeatureWebProvider({
    baseUrl: "https://nona.test",
    apiKey: "frontend-key",
    environmentId: "production",
    pollIntervalMs: 0,
    fetch: async () => jsonResponse({ error: "Server error" }, 503),
  });
  t.after(() => provider.onClose());

  await assert.rejects(() => provider.initialize(), (thrown) => {
    assert.equal(thrown.code, undefined);
    assert.equal(thrown.status, 503);
    return true;
  });
});

test("polling refreshes in the background and stops on close", async () => {
  let served = 0;
  const { client, calls } = stubClient(() => {
    served += 1;
    return snapshotResponse({
      enabled: { value: served > 1 ? "false" : "true", contentType: "boolean" },
    });
  });
  const provider = createNonaOpenFeatureWebProvider(client, {
    pollIntervalMs: 10,
  });
  const domain = domainFor("polling");

  const events = [];
  provider.events.addHandler(ProviderEvents.ConfigurationChanged, (details) => {
    events.push(details);
  });

  await OpenFeature.setProviderAndWait(domain, provider);
  const ofClient = OpenFeature.getClient(domain);
  assert.equal(ofClient.getBooleanValue("enabled", true), true);

  // The poll timer is unref'd, so wait on one that holds the loop open.
  await waitFor(() => events.length > 0);
  assert.deepEqual(events[0].flagsChanged, ["enabled"]);
  assert.equal(ofClient.getBooleanValue("enabled", true), false);

  await provider.onClose();
  const afterClose = calls.length;
  await wait(40);
  assert.equal(calls.length, afterClose, "close must stop the poll timer");
});

test("a failed release refresh keeps the last good snapshot and remains a provider error", async () => {
  const logged = [];
  const { client } = stubClient((call) =>
    call === 1
      ? snapshotResponse(snapshot)
      : jsonResponse({
        title: "Conflict",
        status: 409,
        detail: "No active release",
        errorCode: "active_release_not_configured",
      }, 409),
    { useReleases: true },
  );
  const provider = createNonaOpenFeatureWebProvider(client, {
    pollIntervalMs: 10,
    logger: {
      error: (message) => logged.push(message),
      warn: () => {},
      info: () => {},
      debug: () => {},
    },
  });
  const domain = domainFor("resilient");

  await OpenFeature.setProviderAndWait(domain, provider);
  const ofClient = OpenFeature.getClient(domain);

  await waitFor(() => logged.length > 0);

  assert.equal(ofClient.getBooleanValue("enabled", false), true);
  assert.match(logged[0], /could not refresh/);

  await provider.onClose();
});

test("evaluations report not-ready before initialize and after close", async () => {
  const { client } = stubClient(() => snapshotResponse(snapshot));
  const provider = createNonaOpenFeatureWebProvider(client, {
    pollIntervalMs: 0,
  });

  const before = provider.resolveBooleanEvaluation("enabled", false);
  assert.equal(before.errorCode, ErrorCode.PROVIDER_NOT_READY);

  await provider.initialize();
  assert.equal(
    provider.resolveBooleanEvaluation("enabled", false).value,
    true,
  );

  await provider.onClose();
  const after = provider.resolveBooleanEvaluation("enabled", false);
  assert.equal(after.errorCode, ErrorCode.PROVIDER_NOT_READY);
});

test("a context change refetches the snapshot", async () => {
  const { client, calls } = stubClient((call) =>
    snapshotResponse({
      enabled: { value: call === 1 ? "true" : "false", contentType: "boolean" },
    }),
  );
  const provider = createNonaOpenFeatureWebProvider(client, {
    pollIntervalMs: 0,
  });
  const domain = domainFor("context");

  await OpenFeature.setProviderAndWait(domain, provider);
  const ofClient = OpenFeature.getClient(domain);
  assert.equal(ofClient.getBooleanValue("enabled", false), true);

  await OpenFeature.setContext(domain, { targetingKey: "user-1" });

  assert.equal(calls.length, 2);
  assert.equal(ofClient.getBooleanValue("enabled", true), false);

  await provider.onClose();
});

test("polling recovers from failed initialization and continues refreshing", async (t) => {
  const { client, calls } = stubClient((call) => {
    if (call <= 2) return jsonResponse({ error: "Unavailable" }, 503);
    return snapshotResponse({
      enabled: { value: call === 3 ? "true" : "false", contentType: "boolean" },
    });
  });
  const provider = createNonaOpenFeatureWebProvider(client, { pollIntervalMs: 10 });
  t.after(() => provider.onClose());
  const domain = domainFor("init-recovery");
  const ofClient = OpenFeature.getClient(domain);
  const readyValues = [];
  provider.events.addHandler(ProviderEvents.Ready, () => {
    readyValues.push(ofClient.getBooleanValue("enabled", false));
  });

  await assert.rejects(OpenFeature.setProviderAndWait(domain, provider));
  assert.equal(ofClient.providerStatus, "ERROR");
  await waitFor(() => readyValues.length > 0);
  assert.equal(ofClient.providerStatus, "READY");
  assert.deepEqual(readyValues, [true], "snapshot must be available at recovery");
  await waitFor(() => calls.length >= 4);
  assert.equal(ofClient.getBooleanValue("enabled", true), false);
  assert.equal(readyValues.length, 1, "normal refreshes must not emit Ready");
});

test("disabled polling allows manual recovery without starting a timer", async (t) => {
  const { client, calls } = stubClient((call) => {
    if (call === 1) throw new TypeError("Failed to fetch");
    return snapshotResponse(snapshot);
  });
  const provider = createNonaOpenFeatureWebProvider(client, { pollIntervalMs: 0 });
  t.after(() => provider.onClose());
  const domain = domainFor("manual-recovery");
  await assert.rejects(OpenFeature.setProviderAndWait(domain, provider));
  await wait(40);
  assert.equal(calls.length, 1);

  await provider.refresh();
  const ofClient = OpenFeature.getClient(domain);
  assert.equal(ofClient.providerStatus, "READY");
  assert.equal(ofClient.getBooleanValue("enabled", false), true);
  await wait(40);
  assert.equal(calls.length, 2);
});

test("fatal initialization failures do not start polling", async (t) => {
  for (const status of [401, 404]) {
    const { client, calls } = stubClient(() => jsonResponse({ error: "Rejected" }, status));
    const provider = createNonaOpenFeatureWebProvider(client, { pollIntervalMs: 10 });
    t.after(() => provider.onClose());
    await assert.rejects(provider.initialize(), { code: ErrorCode.PROVIDER_FATAL });
    await wait(40);
    assert.equal(calls.length, 1);
  }
});

test("closing during a recovery request discards its result and stops retries", async (t) => {
  let complete;
  const { client, calls } = stubClient((call) => {
    if (call === 1) return jsonResponse({ error: "Unavailable" }, 503);
    return new Promise((resolve) => { complete = resolve; });
  });
  const provider = createNonaOpenFeatureWebProvider(client, { pollIntervalMs: 10 });
  t.after(() => provider.onClose());
  const events = [];
  provider.events.addHandler(ProviderEvents.Ready, () => events.push("ready"));
  provider.events.addHandler(ProviderEvents.ConfigurationChanged, () => events.push("changed"));
  await assert.rejects(provider.initialize());
  await waitFor(() => complete !== undefined);
  await provider.onClose();
  complete(snapshotResponse(snapshot));
  await wait(40);

  assert.equal(calls.length, 2);
  assert.deepEqual(events, []);
  assert.equal(provider.resolveBooleanEvaluation("enabled", false).errorCode, ErrorCode.PROVIDER_NOT_READY);
});

function wait(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function waitFor(predicate, timeoutMs = 2000) {
  const deadline = Date.now() + timeoutMs;
  while (!predicate()) {
    if (Date.now() > deadline) {
      throw new Error("Timed out waiting for a condition.");
    }

    await wait(5);
  }
}
