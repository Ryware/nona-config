# Swift SDK validation — 2026-09-06

Implemented in `feature/kotlin-client` alongside the Android SDK.

| Check | Result |
| --- | --- |
| SwiftPM unit tests on macOS | 17 passed |
| Swift 5 language mode with complete concurrency checks and warnings as errors | Passed |
| Swift 6 language mode with warnings as errors | Passed |
| iPhone 17 Pro, iOS 26.5 Simulator | 23 passed, 0 failed, 0 skipped |
| iOS test build with Swift warnings as errors | Passed, no warnings reported |
| CocoaPods `pod lib lint`, iOS + macOS, Swift 5.9 | Passed |
| SwiftPM sample build and launch | Passed |
| CocoaPods sample build and launch | Passed |
| Documentation build | 60 pages built |
| Workflow actionlint, Python syntax and git diff checks | Passed |

The 23 simulator tests comprise 17 unit tests, 4 integration tests and 2 UI tests.
Integration tests use a real disposable Nona backend with two projects, frontend
and backend keys, and pinned releases. Transport tests use deterministic local
fault endpoints. UI tests enter a connection, fetch without activating, activate,
restart and restore the cache, and handle invalid URLs without crashing.

Unit coverage includes malformed/default values, finite numeric parsing, cache
identity and corruption, atomic file replacement/reset, pending ETag rollback,
304 timestamp persistence, reset/cancellation races, serialized fetches, clock
rollback, independent update subscribers and percent-encoded selectors.

Security controls reviewed in source include frontend-only key guidance, no logging
of credentials or bodies, SHA-256 cache identity, HTTPS/platform trust, redirect
rejection, streaming response limits, no shared cookies/credentials/cache, and
no publishing credentials in PR CI. Storage failures are explicitly best effort;
client flags are not an authorization mechanism.

Deployment targets are iOS 15 and macOS 12. Runtime testing used iOS 26.5 and the
installed macOS/Xcode 26.6 environment; minimum-version physical devices were not
available. CocoaPods lint reported environment notes about the local Metal toolchain
search path and skipped App Intents extraction; validation itself passed. There was
no CocoaPods registry publication, release tag, App Store upload or GitHub-hosted CI
run. The checked-in workflow is prepared for subsequent runs after pushing.
