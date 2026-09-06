# Swift SDK validation — 2026-09-06

## Latest architecture follow-up

The subsequent working-tree audit based on `6070d8e` is recorded in
[ARCHITECTURE.md](ARCHITECTURE.md). It adds notification coalescing, serialization
and sample lifecycle fixes, plus independent shared QA tooling. Current results:
25 unit tests under Thread Sanitizer, 31 iOS simulator tests, Swift 6 compilation,
CocoaPods lint for iOS/macOS with Swift 5.9, CocoaPods sample Release build,
8 Python contract tests, Android instrumentation APK compilation and 60-page
documentation build all pass. Android emulator runtime tests were not rerun.
The automated security scan described below predates these changes.

## Earlier final review

Final review performed on `feature/swift-client`; Swift SDK based on commit `004cc7e`.
The fixes below are a subsequently reviewed and tested working-tree patch.

| Check | Result |
| --- | --- |
| SwiftPM unit tests on macOS | 21 passed |
| Swift 5 language mode with complete concurrency checks and warnings as errors | Passed |
| Swift 6 language mode with warnings as errors | Passed |
| Thread Sanitizer on macOS | 21 passed; no sanitizer diagnostics |
| iPhone 17 Pro, iOS 26.5 Simulator | 27 passed, 0 failed, 0 skipped |
| iOS test build with Swift warnings as errors | Passed, no warnings reported |
| CocoaPods `pod lib lint`, iOS + macOS, Swift 5.9 | Passed |
| SwiftPM sample build and launch | Passed |
| CocoaPods sample Release build and launch | Passed; Swift warnings as errors |
| Documentation build | 60 pages built |
| Workflow actionlint, Python syntax and git diff checks | Passed |

The 27 simulator tests comprise 21 unit tests, 4 integration tests and 2 UI tests.
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
run performed by this review. The checked-in workflow is prepared for subsequent runs.

## Final review corrections

- Separated cache serialization from the state lock so slow read/write/clear operations
  cannot block synchronous value reads. Lock ordering remains cache then state;
  reset and persistence still share serialization to prevent stale disk writes.
- Added a cancellation check before committing restored cache state. Previously,
  cancellation during a cache read still restored values.
- Reject all control characters in API keys, including tab and DEL.
- Corrected CocoaPods/SPM installation instructions to `feature/swift-client` and
  documented cancellation's commit boundary accurately.
- Simulator QA runner now treats Swift warnings as errors.

Two new regression tests failed on the original implementation: blocked-cache
read/write/clear prevented getters from completing, and cancelled initialization
restored values. Both passed after correction. Further tests cover failed refreshes
preserving active values and response limits on injected transports.

The security diff scan reviewed all 32 changed files in `f0c0ffa..004cc7e`, including
12 source inventory items and packaging, CI, generated projects, plists and docs.
It included an independent architecture review. No confirmed security findings.
Subsequent fixes were manually reviewed and tested separately; the immutable scan
must not be represented as an automatic scan of the later working tree.

Scan ID: `4c5a28fd-4b68-4e58-a7c2-ddf883d11e70`.
Runtime evidence: `/tmp/nona-swift-review-ios.xcresult`,
`/tmp/nona-swift-review-tests.log`, `/tmp/nona-swift-review-tsan.log`,
`/tmp/nona-swift-review-swift6.log`, `/tmp/nona-swift-review-pod-lint.log`.
These are local evidence paths, not portable repository artifacts.
