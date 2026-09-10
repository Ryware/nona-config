# Kotlin SDK architecture audit — 2026-09-06

Scope: the Kotlin SDK runtime, Java bridge, sample lifecycle, manifests, Gradle
publication metadata, CI and shared QA consumers in the working tree on
`feature/swift-client`. This is not an audit of every backend or other SDK component.
Earlier Swift/shared-QA changes were preserved. No release or remote publication
was performed by this audit.

## Result and corrections

The Swift notification defect also existed in Kotlin: a `MutableSharedFlow` with
one buffered event and `DROP_OLDEST` discarded unread changed-key sets. A consumer
watching a particular key could miss its change or deletion permanently.

| Finding | Correction |
| --- | --- |
| Slow collectors lost changed/deleted keys. | `ConfigUpdates` keeps a separate pending key union and conflated wake-up channel per collector. It cleans subscriptions on cancellation, has no replay or background scope, and invokes collectors outside locks. |
| Disk I/O held the state monitor and blocked synchronous activation. | Added a separate cache mutex. Read/parse/write/clear work stays on the I/O dispatcher; state commits remain short and use the state monitor. |
| Cancelling initialization during a blocking cache read still installed its result. | Check coroutine cancellation at the state commit boundary. Fetch/reset commits use the same discipline. |
| A custom HTTP client could bypass the configured response limit. | Check returned UTF-8 body size before parsing, in addition to the default transport's streaming limit. Custom transports must still bound allocation while reading. |
| Option validation allowed header control characters, dot path segments and invalid ports. | Reject those values at construction; invalid arguments cannot reach network setup. |
| Data-class diagnostics printed the API key. | Override `NonaOptions.toString()` to redact the key. This does not make a key embedded in an application secret. |
| Old sample request completions could overwrite the latest operation's message. | Track operation identity and ignore obsolete or post-destruction UI completions. Use current inset APIs with an API 24–29 fallback. |
| JSON/status fault scenarios shared the deliberate 100 ms timeout. | Keep 100 ms only for the slow-response test; use 5 seconds for ordinary connect/read operations and improve assertion diagnostics. |

Five focused regression tests failed on the original implementation and passed
after correction. Two more cover concurrent publishers/subscription cancellation
and redacted diagnostics/invalid ports. A device regression exercises slow
collectors against Android's actual coroutine/runtime implementation.

## Architecture and invariants

`NonaConfig` orchestrates immutable snapshots, defaults, injected transport,
store and clock. A coroutine mutex (`fetchLock`) serializes fetches. Getters use volatile references and do not wait for disk.
Multiple getter calls are not a transaction across concurrent activation; callers
should activate at a suitable application lifecycle boundary.

The lock order is cache mutex, then state monitor. Activation takes only the state
monitor. Persistence and reset share cache serialization; reset advances the
request generation, preventing older HTTP results from restoring cleared state.
The built-in file store also serializes its own operations for reuse of that store
instance. This is not a transaction across different client instances or processes:
reuse a long-lived client per configuration identity.

Initialization restores the last successful fetch, including previously pending
values. Fetch stages data, activation exposes it, and 304 refreshes the matching
snapshot timestamp. Failures preserve previously active values; malformed typed
values fall back to defaults. Cache identity includes normalized origin, key,
environment, prefix and release. Atomic file replacement prevents partial writes;
cache persistence is best effort and custom stores must honor that contract.

Cancellation prevents commit when observed before the commit boundary. It does
not undo a completed commit. The synchronous `HttpURLConnection` transport may
continue until its connect/read timeout; these are not a total request deadline.
Applications requiring a total deadline should inject a transport that provides
one. No global coroutine scope is added for notifications. Java futures cancel
their corresponding coroutine, with the same blocking-operation limitation.

Notifications are invalidations, not a durable event log: pending key unions can
grow with distinct unread keys, emissions may be combined or observed out of
activation order under concurrent publishers, and consumers must read current
values. New subscriptions do not replay history. Reset/default changes are not
activation events. Downstream operators such as `conflate` can intentionally
change delivery guarantees and should not be added when every key matters.

## API compatibility

`configUpdates` now returns `Flow<Set<String>>` instead of `SharedFlow<Set<String>>`.
Ordinary `.collect` calls are source compatible, but explicit `SharedFlow` types,
SharedFlow-specific operators and already compiled consumers need migration or
recompilation. This is a JVM binary API change: select a new release version under
the project's compatibility policy and document it; never replace an existing
published artifact. The package version was not changed by this audit.

Using the supported `flow` builder avoids inheriting from the third-party
`SharedFlow` interface, whose inheritance contract is explicitly unstable.
See [the Kotlin SharedFlow contract](https://kotlinlang.org/api/kotlinx.coroutines/kotlinx-coroutines-core/kotlinx.coroutines.flow/-shared-flow/).

## Security and delivery boundaries

The default transport rejects redirects, disables shared HTTP caching and bounds
response bytes before JSON parsing. HTTPS relies on platform trust; release
network policy belongs to the consuming app. Use HTTPS and frontend-scoped keys;
client flags are not authorization. HTTP remains available for debug/local QA.
This transport does not promise the same cookie/authenticator isolation as a
private URLSession: process-wide Android networking configuration is an integration
boundary. SDK code does not modify global networking handlers.

The cache is app-private configuration data, not an encrypted or authenticated
secret store. Custom stores/transports are trusted extension points. A sample's
exported launcher and intent-driven connection setup are QA interfaces, not a
recommended production login/provisioning design.

The AAR excludes the sample and Python tooling; coroutine dependencies are exposed
through Gradle `api` because they occur in public signatures. Added the official
SHA-256 for the pinned Gradle distribution. No dependency upgrade, signing-key
change, Maven Central upload or automated vulnerability inventory was performed.
CI should run on the exact final commit before publishing. The release workflow's
Kotlin gate builds/tests/lints but does not itself require the separate emulator
matrix job; successful emulator CI remains a release requirement.

## Verification

- JVM: 38 tests passed, including Java API and seven new architecture regressions.
- Final Android API 24 and API 37 emulator suites: 11 tests passed on each.
- SDK release AAR, sample Debug/Release and instrumentation APK builds passed.
- SDK lint: zero errors, seven dependency-update advisories; sample lint: no issues.
- Local Maven publication and sample Release consumption of its AAR passed.
- Shared QA contracts: eight passed; workflow actionlint and diff checks passed.
- Documentation: 60 pages built.

An API 37 run exposed the fault-test timeout coupling described above. That run failed and is retained in the evidence. Final device results and remaining runtime
limits are recorded in VALIDATION.md. No physical-device, sustained load/memory
profile or new automated security scan was performed. Earlier scan evidence only
applies to the immutable revisions stated in the earlier report.

Local evidence: `/tmp/nona-kotlin-audit-before.log`,
`/tmp/nona-kotlin-audit-final-build.log`, `/tmp/nona-kotlin-audit-publication.log`,
`/tmp/nona-kotlin-audit-consumer.log`, `/tmp/nona-kotlin-audit-docs.log`.
Temporary paths are not portable repository artifacts.
