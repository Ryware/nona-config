# Swift client architecture audit — 2026-09-06

Reviewed the working tree on `feature/swift-client`, based on `6070d8e`.
Scope: all Swift SDK source, concurrency and persistence boundaries, public API,
sample lifecycle, package manifests, Swift CI, and the shared QA tools with their
Swift/Android consumers. This is not a repository-wide audit of the backend,
web application, TypeScript client, or Android SDK implementation.

## Dependency boundaries

The application depends on `NonaConfig` and public value types. `NonaConfig`
coordinates configuration state through injected `NonaHTTPClient`,
`NonaSnapshotStore`, and clock dependencies. The default transport owns an
ephemeral URLSession; the default store owns an application-private cache file.
Neither the sample nor QA tools are part of the distributed library. SwiftPM
and CocoaPods compile the same sources without third-party runtime dependencies.
Shared Python fixtures live in `client/qa`; neither SDK depends on the other SDK.

This separation supports deterministic tests without adding a framework or
making synchronous configuration reads require an actor hop. Replacing the
whole client with an actor would change the public read API and is not needed
to address the defects found here.

## Correctness invariants

- Fetches are serialized by `FetchGate`; cancelled waiters cannot initiate HTTP.
- A successful fetch stages values; activation publishes them. Initialization
  restores the last successful fetch, including a previously unactivated fetch.
- State mutation is protected by one lock. Disk operations run off the UI actor
  and use separate serialization; getters do not wait for disk I/O.
- The lock order is cache serialization followed by state. Reset shares cache
  serialization and advances a generation so earlier requests cannot repopulate it.
- Cancellation is checked before state commit. Cancellation after commit does not
  roll back already committed state or synchronous cache operations.
- Each subscriber has its own coalesced invalidation stream. Notifications mean
  "read these keys again", not an ordered or durable history of snapshots.
- Cache identity includes server, key, environment, release and prefix. Cache
  failures are best effort and do not discard valid in-memory values.

## Findings corrected

| Finding | Correction and evidence |
| --- | --- |
| A slow subscriber lost earlier changed keys, including deleted keys, when the one-element buffer was replaced. | Added `UpdateSubscriber`, which serializes emissions and merges the dropped set. A regression test failed before the fix and passes after it; a concurrent-producer test checks 100 distinct keys. |
| Thread Sanitizer diagnosed concurrent access involving the generic lock's zero-sized `Void` storage during emission stress. | Introduced `SerialAccess` for serialization without an `inout` value, also used for cache serialization. All 25 tests subsequently pass under Thread Sanitizer without suppressions. |
| Completion of an old cancelled sample operation could overwrite a newer operation's status. | Added operation identity checks and cancellation cleanup; the task captures the model weakly. Sample builds and both existing UI scenarios pass. A targeted UI cancellation stress scenario was not run. |
| QA setup was owned by the Android tree despite serving both SDKs; fixture validation differed and Android could install an APK before validating input. | Moved common setup into `client/qa`, centralized validation before subprocess side effects, updated fixture names and both runners/CI. Eight contract tests cover invalid input and command argument mapping. |
| The shared fault server redirected to an Android-only host alias. | Select a redirect target from an explicit loopback/Android-emulator allowlist. Unknown hosts fall back to loopback. Contract tests cover aliases; iOS transport tests pass against the server. |

Additional regressions confirm reset cannot resurrect a pending disk write and
an update stream does not retain its client. Public method signatures remain
unchanged. Old QA databases/fixtures must be recreated after the project rename.

## Security and operational contracts

The default transport rejects redirects, applies timeouts and streaming response
size limits, and avoids shared HTTP cookies, credentials and cache. TLS uses the
platform trust implementation. API keys reject control characters; errors avoid
including request credentials or response bodies. Tests exercise wrong key scopes,
project separation, malformed responses, oversized bodies and transport timeouts.

Applications must use HTTPS and frontend-scoped keys. A key shipped in an app is
public; client flags must never authorize privileged server actions. HTTP remains
available for local development, subject to the application's platform policy.
Injected transports are responsible for their own redirect, timeout and streaming
limits; the client additionally checks their returned body size. Custom stores
must be thread-safe and application-private. Cache identity is separation, not
encryption or authentication of locally modified files.

Use one long-lived client per configuration identity. Cache serialization is per
client/store instance, not a cross-process transaction. Unread notification sets
can grow with the number of distinct changed keys even though only one notification
is buffered; consumers should continuously consume or cancel their subscription.
Reset/default changes do not emit activation notifications. These semantics are
deliberate constraints, not a general-purpose event bus or distributed database.

## Validation and release gates

- 25 Swift unit tests pass under Thread Sanitizer on macOS; Swift 6 language-mode
  compilation passes with warnings treated as errors.
- iOS 26.5 Simulator: 31 passed, zero failed/skipped (25 unit, 4 real-server
  integration, 2 UI); Swift warnings treated as errors.
- CocoaPods sample Release build passes with Swift warnings treated as errors.
- `pod lib lint NonaClient.podspec --platforms=ios,osx --swift-version=5.9`
  passes without `--allow-warnings`. CocoaPods reports toolchain/App Intents notes
  from the local Xcode environment, as in the earlier validation.
- Shared Python contracts: 8 passed. Android instrumentation APK compiles;
  Android emulator runtime tests were not rerun in this audit.
- Documentation builds all 60 pages; both workflows pass actionlint.

Local evidence: `/tmp/nona-architecture-ios.xcresult`,
`/tmp/nona-architecture-tsan.log`, `/tmp/nona-architecture-swift6.log`,
`/tmp/nona-architecture-android-build.log`, `/tmp/nona-architecture-docs.log`,
`/tmp/nona-architecture-pod-lint-strict.log`.
These temporary files are not portable repository artifacts.

Before a public release, run hosted CI on the exact final commit, confirm supported
minimum OS runtime behavior (iOS 15/macOS 12), and smoke-test installation from the
actual immutable release tag in a clean consumer project. Keep podspec version and
the root SwiftPM SemVer tag aligned. Registry publication and React Native npm
compatibility are separate release work; this audit does not establish them.
Physical-device testing, sustained performance profiling and a new automated
security scan of this working tree were not performed. Earlier security-scan
evidence in VALIDATION.md applies only to its stated immutable revision.

No additional blocking defect was identified within this scope after the fixes
and checks above. This is evidence for a release candidate, not a guarantee of
zero defects or a claim that the package has been published.
