# NonaClient — Swift

Remote configuration for iOS 15+ and macOS 12+. Swift 5.9+, including Swift 6 strict
concurrency. No third-party runtime dependencies; uses Foundation, CryptoKit and URLSession.

## Install

The Swift SDK is currently developed on `feature/swift-client`; it has not been
published to the CocoaPods registry or released under a version tag.

### CocoaPods

Add to your application's Podfile:

```ruby
platform :ios, '15.0'
use_frameworks!
pod 'NonaClient', :git => 'https://github.com/Ryware/nona-config.git', :branch => 'feature/swift-client'
```

Run `pod install` and open the generated `.xcworkspace`. For a local checkout,
use `pod 'NonaClient', :path => '/path/to/nona-config'` instead.

### Swift Package Manager

In Xcode, choose **File → Add Package Dependencies**, enter
`https://github.com/Ryware/nona-config.git`, select branch `feature/swift-client`,
and add the **NonaClient** product. The package manifest is at the repository root.
For local development, add this repository as a local package.

After an actual release, applications can pin an immutable semantic version. The
podspec expects a matching tag such as `0.1.0`; this change does not create that tag
or publish a pod. Commit `Podfile.lock` / `Package.resolved` in consuming applications.

## Use

```swift
import NonaClient

let options = try NonaOptions(
    baseURL: URL(string: "https://nona.example.com")!,
    environmentID: "Production",
    apiKey: frontendKey
)
let config = NonaConfig(options: options)
config.setDefaults([
    "Features:Checkout": false,
    "Limits:Retries": 3,
    "Copy:Title": "Checkout"
])

// In a task owned by your app's lifecycle:
try await config.initialize() // disk only
do {
    try await config.fetchAndActivate()
} catch is CancellationError {
    // The owning task was cancelled.
} catch {
    // Keep the last good configuration/defaults; retry at a suitable boundary.
}

// Synchronous, thread-safe reads; no network request.
let enabled = config.getBoolean("Features:Checkout")
let retries = config.getLong("Limits:Retries") // Int64
let title = config.getString("Copy:Title")
```

Keep one client per connection/selector combination. Use structured concurrency
and cancel the owning task when its result is no longer needed.

## Fetch, activate, reset

- `fetch()` downloads and caches a pending snapshot. Active reads stay unchanged.
- `activate()` synchronously applies pending values and reports whether they changed.
- `fetchAndActivate()` performs both steps.
- `initialize()` restores the last successful fetch, even if it was not activated
  before the previous process stopped. Call it before your first fetch/read at startup.
- `reset()` clears memory and cache and invalidates in-flight responses. It preserves
  defaults. Requests invalidated by reset return `.discarded`.

Concurrent fetches are serialized. Cancellation is checked before committing state;
cancellation after that point does not undo a completed commit. Queued cancelled
fetches do not start network requests. Activation emits changes via
`updates()`; defaults/reset do not emit activation events.

```swift
for await changedKeys in config.updates() {
    // Dispatch UI updates to MainActor where needed.
}
```

Each subscriber gets its own stream, with a newest-one buffer. This is a notification
stream rather than a durable event log; read current values when notified. Avoid
retaining the client indefinitely from a task that is waiting on its own stream.

## Values and defaults

Typed getters use remote value → configured default → type zero. Malformed remote
numbers/booleans fall back as well. Integers are Int64; doubles must be finite.
`getSource` reports the raw entry's origin, which may differ from the source actually
used by a typed fallback. `resolveString`, `resolveBoolean`, `resolveLong` and
`resolveDouble` return `NonaResolution` with source/content type or a failure reason:
`notReady`, `notFound`, or `typeMismatch`.

JSON entries are exposed as raw strings; decode them into your application's types.
Every device reading the same environment/selectors gets the same configuration;
there is no per-user targeting in this SDK.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `baseURL`, `environmentID` | required | Server and environment |
| `apiKey` | nil | Frontend-scoped key |
| `prefix` | nil | Restrict returned keys |
| `releaseVersion` | nil | Pin an exact release or release line |
| `minimumFetchInterval` | 12 hours | Seconds between successful fetches |
| `requestTimeout` | 10 seconds | Request/resource timeout of the default transport |
| `maxResponseBytes` | 8 MiB | Maximum decoded response bytes |

Use `try await config.fetch(minimumFetchInterval: 0)` to bypass throttling. Failed
requests do not advance the throttle timestamp. ETag revalidation uses the latest
pending or active snapshot, including when the server rolls back a release.

## Security and storage

Use HTTPS directly: automatic redirects are rejected. The default transport uses
an ephemeral session without shared cookies, credential storage or HTTP caching.
It limits the response as it arrives, including responses without Content-Length.
The SDK does not weaken App Transport Security or log keys/response bodies.

Only ship frontend-scoped API keys. APKs/IPAs and their frontend configuration are
inspectable; do not put secrets in frontend entries. Backend-only keys are rejected
by the snapshot endpoint with HTTP 404.

The default cache is under Application Support/NonaClient, uses atomic writes, and
is excluded from backup after a successful write. Its SHA-256 identity includes the
normalized server URL, key, environment, prefix and release. Raw keys are not stored
in cache names/metadata. Corrupt or mismatched caches are ignored. Cache I/O is best
effort: device/storage failures can prevent persistence or deletion, so cached flags
must never be an authorization boundary. For macOS apps, use a sandbox or supply a
store in an appropriate application-private directory.

Custom implementations must be thread-safe:

```swift
let config = NonaConfig(options: options, store: InMemorySnapshotStore(), http: myHTTPClient)
```

`NonaHTTPClient` is async and Sendable. Custom transports must handle cancellation,
timeouts, redirects and streaming limits; the SDK also checks returned body size.
`NonaSnapshotStore` is synchronous and Sendable; its methods run off the main actor
and do not hold the lock used by synchronous value reads.
Do not call back into the same NonaConfig from a custom store.

## Build and verify

From the repository root:

```sh
swift test -Xswiftc -strict-concurrency=complete -Xswiftc -warnings-as-errors
swift build -Xswiftc -swift-version -Xswiftc 6 -Xswiftc -warnings-as-errors
pod lib lint NonaClient.podspec --platforms=ios,osx --swift-version=5.9
pod install --project-directory=client/swift/Sample
```

Open `client/swift/Sample/NonaSample.xcworkspace`:

- `NonaSample` links the local Swift package.
- `NonaPodSample` links the local CocoaPod.

Both show connection fields, active values and lifecycle buttons. They are developer
samples, not App Store products. Local networking is enabled only in their plists.
To regenerate the checked-in Xcode project, run
`xcodegen generate --spec client/swift/Sample/project.yml`, then rerun `pod install`.

See [qa/README.md](qa/README.md) for simulator tests against a real disposable backend.
