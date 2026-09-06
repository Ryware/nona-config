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
    config.fetchAndActivate() // refresh from the network
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

`activate()` returns `true` when values actually changed, and `configUpdates` emits the changed keys:

```kotlin
lifecycleScope.launch {
    config.configUpdates.collect { changed ->
        if ("Features:Checkout" in changed) invalidateUi()
    }
}
```

## Where values come from

Reads fall back in order: **activated remote value → your in-app default → the type's zero value** (`false`, `0`, `""`). `getSource(key)` reports which applied.

`getBoolean`, `getString`, `getLong` and `getDouble` never throw and never explain a fallback. When the reason matters, `resolveBoolean`, `resolveString`, `resolveLong` and `resolveDouble` return a `NonaResolution` carrying `NOT_READY`, `NOT_FOUND`, `TYPE_MISMATCH` or `PARSE_ERROR`. `resolveString` hands back the raw stored string, so JSON values arrive unparsed for you to decode.

## Throttling and offline

`fetch()` skips the network entirely when the previous fetch was more recent than `minimumFetchInterval`, returning `FetchStatus.THROTTLED`. The default is 12 hours. Pass an explicit interval to override it during development:

```kotlin
config.fetch(Duration.ZERO)
```

Fetches send the snapshot's `ETag`, so an unchanged environment answers `304` with no body. The steady-state cost does not grow with the number of keys.

Each successful fetch is written to the app's private files, keyed by environment, prefix and pinned release so different configurations cannot collide. `initialize()` restores it, which is what lets a cold start on a plane show real values instead of defaults. A corrupt cache is ignored rather than fatal.

## Options

| Option | Default | Description |
| --- | --- | --- |
| `baseUrl` | — | Nona server URL |
| `environmentId` | — | Environment to read |
| `apiKey` | none | Frontend-scoped key |
| `releaseVersion` | none | Pin to `1.4.0`, or a line such as `1.4.x` |
| `prefix` | none | Only load keys under this prefix |
| `minimumFetchInterval` | 12 hours | Throttle window |
| `connectTimeout` / `readTimeout` | 10 seconds | Network timeouts |

## Use HTTPS

The client does not follow redirects across protocols, which is standard `HttpURLConnection` behaviour. If your server answers `http://` with a redirect to `https://`, the client surfaces the redirect status as an error rather than following it. Configure `baseUrl` with `https://` directly.

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
