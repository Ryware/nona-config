---
title: Android client
description: Read Nona config values and feature flags on Android from Kotlin or Java, with in-app defaults, offline caching, and a separate fetch and activate flow.
---

Package: `com.nonaconfig:nona-client` (Kotlin, usable from Java)

Requirements:

- Android 7.0 (API 24) or newer
- A **frontend-scoped** Nona API key

Values live in memory and are read synchronously, downloading is separate from applying, and the last good snapshot survives a restart — so a cold start with no network still shows real config.

## What to set up first

1. open `Projects`, then the project and the target environment
2. create a parameter such as `Features:Checkout`
3. mark it **frontend** scope so devices can read it
4. create an API key with the **frontend** scope

## Usage

```kotlin
val config = NonaConfig.create(
    context,
    NonaOptions(
        baseUrl = "https://nona.example.com",
        environmentId = "production",
        apiKey = BuildConfig.NONA_FRONTEND_KEY,
    ),
)

config.setDefaults(mapOf("Features:Checkout" to false))

lifecycleScope.launch {
    config.initialize()       // restore the cached snapshot from disk
    try {
        config.fetchAndActivate() // refresh from the network
    } catch (error: NonaException) {
        // Keep the cached values/defaults while offline; retry at a suitable boundary.
    }
}

// Synchronous, safe on the main thread.
if (config.getBoolean("Features:Checkout")) {
    showCheckout()
}
```

## Fetch and activate

Downloading and applying are deliberately separate:

- `fetch()` downloads the snapshot into a pending slot. Reads are unchanged.
- `activate()` promotes those values into the active set. Reads change now.
- `fetchAndActivate()` does both.

The split keeps config from changing under a user mid-session. Fetch whenever; activate at a safe moment, such as the next launch or when the user returns to a neutral screen.

`reset()` discards in-flight fetch results as well as clearing memory and disk; those fetches return `FetchStatus.DISCARDED`.

`activate()` returns `true` when values actually changed. `configUpdates` is a
`Flow<Set<String>>` with an independent subscription for each collector. Slow
collectors receive the union of unread changed keys, including deletions. Read
current values when notified; notifications are not a snapshot history. New
collectors receive no replay, and reset/default changes do not emit notifications.
Cancel collection with the screen's lifecycle. Pending sets grow with the number
of distinct unread keys, not the number of activations.

`Flow` is the API for the first release. Earlier development snapshots exposed
`SharedFlow`; the SDK has no existing users requiring migration.

Collect updates:

```kotlin
lifecycleScope.launch {
    config.configUpdates.collect { changed ->
        if ("Features:Checkout" in changed) invalidateUi()
    }
}
```

## Where values come from

Reads fall back in order: **activated remote value → your in-app default → the type's zero value** (`false`, `0`, `""`). `getSource(key)` reports the raw entry origin. A typed getter may use a default instead if the remote value cannot be parsed.

`getBoolean`, `getString`, `getLong` and `getDouble` never throw and never explain a fallback. When the reason matters, `resolveBoolean`, `resolveString`, `resolveLong` and `resolveDouble` return a `NonaResolution` carrying `NOT_READY`, `NOT_FOUND`, `TYPE_MISMATCH` or `PARSE_ERROR`. `resolveString` hands back the raw stored string, so JSON values arrive unparsed for you to decode.

## Throttling and offline

`fetch()` skips the network entirely when the previous fetch was more recent than `minimumFetchInterval`, returning `FetchStatus.THROTTLED`. The default is 12 hours. Pass an explicit interval to override it during development:

```kotlin
config.fetch(Duration.ZERO)
```

Fetches send the snapshot's `ETag`, so an unchanged environment answers `304` with no body. The steady-state cost does not grow with the number of keys.

Each successful fetch is written to the app's private files, bound to the server URL, API-key fingerprint, environment, source mode, prefix and release selector using SHA-256. The stored identity is checked on restore; caches from another configuration or the old format are ignored. Raw API keys are not written into cache filenames or snapshots. `initialize()` restores it, which is what lets a cold start on a plane show real values instead of defaults. A corrupt cache is ignored rather than fatal.

## Options

| Option | Default | Description |
| --- | --- | --- |
| `baseUrl` | — | Nona server URL |
| `environmentId` | — | Environment to read |
| `apiKey` | none | Frontend-scoped key |
| `useReleases` | `false` | Read immutable releases instead of working parameters |
| `releaseVersion` | none | Pin to `1.4.0`, or a line such as `1.4.x` |
| `prefix` | none | Only load keys under this prefix |
| `minimumFetchInterval` | 12 hours | Throttle window |
| `connectTimeout` / `readTimeout` | 10 seconds | Network timeouts |
| `maxResponseBytes` | 8 MiB | Decoded response limit for the default HTTP transport |

Set `useReleases = true` without a version to follow the active release. A non-empty `releaseVersion` is valid only in release mode. Source selection is fixed at construction, so use another `NonaConfig` instance for a different source or selector. Failed refreshes preserve the last-known-good snapshot and never switch sources.

## Use HTTPS

The default HTTP client rejects all redirects so credentials and configuration stay on the configured server. Set `baseUrl` to the final HTTPS address directly. Responses are limited to 8 MiB by default; increase `maxResponseBytes` only if your snapshots require it. Custom HTTP clients must enforce their own redirect and response-size policies.

Plain `http://` also needs `usesCleartextTraffic` in the app's manifest on Android 9 and newer. That decision belongs to the app, so the library does not declare it.

## The API key is public

Anything shipped inside an APK can be extracted, so treat the key as public and make it frontend-scoped. Nona returns only frontend-scoped entries to that endpoint and refuses backend-only keys outright, with a `404` that is deliberately indistinguishable from an unknown environment so a server key cannot enumerate environments.

See [Client vs server scope](/docs/concepts/client-vs-server-scope).

## No targeting

Every device on an environment receives the same values. Nona has no per-user rules, percentage rollouts, or A/B buckets, so there is no user identity to supply. The closest equivalent is pinning a build to an immutable `releaseVersion`, which keeps an old app version on config it was tested against.

## Swapping the network or storage layer

`NonaHttpClient` and `NonaSnapshotStore` are interfaces. The defaults use `HttpURLConnection`, which is backed by OkHttp on Android and needs no extra dependency, and a file in the app's private storage. Supply your own to reuse an existing OkHttp stack, keep config out of storage, or fake either in tests.

## Related docs

- [Client vs server scope](/docs/concepts/client-vs-server-scope)
- [Feature flags](/docs/feature-flags)
- [JavaScript client](/docs/clients/javascript)
- [OpenFeature](/docs/clients/openfeature)

## Java API

```java
NonaOptions options = NonaOptions.builder("https://nona.example.com", "Production")
    .apiKey(frontendKey)
    .minimumFetchIntervalMillis(3_600_000)
    .build();
NonaConfig config = NonaConfig.create(context, options);
config.initializeAsync()
    .thenCompose(restored -> config.fetchAndActivateAsync())
    .whenComplete((changed, error) -> {
        // Values/defaults remain readable on failure. Dispatch UI updates to the main thread.
    });
```

`initializeAsync`, `fetchAsync`, `fetchAndActivateAsync`, and `resetAsync` return `CompletableFuture` (Android API 24+). Cancelling the future cancels its coroutine; blocking transport may take until its timeout to finish. Kotlin callers can continue using the suspending methods and `Duration` options.

### Concurrency and cancellation

Reuse one client per server/key/environment/selector identity. Fetches serialize;
cache I/O runs on the supplied I/O dispatcher, outside the state lock. Cache
serialization is per client (and per built-in file-store instance), not a
cross-process transaction. Custom stores must be thread-safe and best effort.
Cancellation is checked before committing state; it does not undo a completed
commit or interrupt a blocking transport/store immediately. The default transport
uses connect/read timeouts, not a total wall-clock request deadline. Inject a
transport with a total deadline if your application requires one. Custom transports
must enforce their own redirect and streaming limits; returned bodies are also
checked by the SDK. Use HTTPS and frontend keys in applications.

`getDouble` accepts finite decimal values with optional sign, fraction and
exponent. Integer getters require a decimal integer within the signed 64-bit range.
Language-specific suffixes and hexadecimal syntax are rejected. A missing
or null `contentType` defaults to `text`; a non-string type rejects the snapshot.
