# Kotlin SDK final validation — 2026-09-06

## CI transport follow-up

GitHub run `34043116262` passed JVM checks and API 24, but API 36 failed
connecting to `10.0.2.2:18686` in the `activeRelease` fixture setup, before the
cache assertions. This establishes a connection timeout, not its precise cause.
The runner now uses explicit ADB reverse mappings for the backend and both fault
ports, with `127.0.0.1` URLs. It reuses matching mappings, rejects conflicts before
installing APKs, and records routing/mapping diagnostics on test failure. No SDK
behavior, timeout or test retry policy was changed.

Local API 24 and API 36 suites both passed all 14 tests through the new transport.
All 12 Python contract tests passed. Linux x86_64 hosted CI still needs to confirm
this change; local emulators are ARM64. Evidence: `/tmp/nona-reverse-api24.txt`
and `/tmp/nona-reverse-api36.txt`.


## Latest failure and compatibility checks

46 JVM tests, 14 device tests on each of API 24 and API 37, SDK/sample lint
and local Maven AAR consumer builds pass.
The [shared report](../../qa/RISK-VALIDATION.md) records parser/cache corrections,
shared fixtures, upgrade and process-kill checks, commands and remaining limits.
The Kotlin JVM signature check reports the known SharedFlow-to-Flow binary break;
it requires a release/migration decision and is not a passing gate. Earlier
results below remain historical.

## Latest architecture follow-up

The working-tree follow-up on `feature/swift-client` is detailed in
[ARCHITECTURE.md](ARCHITECTURE.md). It fixes lost notification keys, cache/state
lock coupling, cancellation at commit, custom transport size validation, unsafe
option values, key-bearing diagnostics and stale sample UI completions.

| Current check | Result |
| --- | --- |
| JVM suite including Java API and seven new regressions | 38 passed |
| Android API 24 ARM64 emulator, final suite | 11 passed |
| Android API 37 ARM64 emulator, final suite | 11 passed |
| Release AAR and sample Debug/Release builds | Passed |
| SDK lint | 0 errors; 7 dependency-update advisories |
| Sample lint | No issues |
| Isolated local Maven publication and AAR consumer Release build | Passed |
| Shared Python contracts | 8 passed |
| Documentation | 60 pages built |
| Workflow actionlint and git diff checks | Passed |

The new device test checks slow subscribers retaining changed and deleted keys.
The five initial JVM regression cases failed before fixes and pass afterwards.
An intermediate API 37 run failed the malformed-response assertion while using
100 ms read timeouts for all fault scenarios. The test now reserves that timeout
for the deliberately slow endpoint and uses 5 seconds for ordinary responses;
subsequent complete runs on both API levels pass. The exact transient exception
was not captured by the original assertion, so timeout coupling is the supported
explanation, not proof of a specific exception from that failed run.

Manual API 24 launch after force-stop restored `flag: A`, source `REMOTE` and
fallback retries `3`; the screen was inspected and the emulator crash buffer was
empty. Full manual offline/reset sequences were not repeated in this follow-up.

`configUpdates` changes from `SharedFlow` to `Flow`: consumers must rebuild, and
explicit SharedFlow usage needs migration before releasing a new artifact.
Hosted CI, Maven Central publication, physical-device testing and a new automated
security scan were not performed. The older results below are historical.

Evidence: `/tmp/nona-kotlin-audit-api24-final.txt`,
`/tmp/nona-kotlin-audit-api37-final.txt`, `/tmp/nona-kotlin-audit-final-build.log`,
`/tmp/nona-kotlin-audit-api24.png`, `/tmp/nona-kotlin-audit-consumer.log`.

## Earlier final review

Reviewed the PR 86 changes against development (`be22343`), including runtime,
Java/Kotlin API, cache lifecycle, sample, QA scripts, CI and documentation.
This is a scoped code review and local validation, not a production deployment certification.

## Additional fixes from the final pass

- Reject HTTP redirects and disable shared HTTP caching in the default transport.
- Bound decoded response reads to 8 MiB by default; expose `maxResponseBytes` in Kotlin and Java.
- Report HTTP failures without reading unused response bodies.
- Preserve percent-encoded base paths and allow refresh after wall-clock rollback.
- Validate per-fetch interval overrides.
- Validate sample connection inputs before persisting them and handle invalid URLs without crashing.
- Create QA credential files with private permissions and propagate the fixture server port to devices.
- Strengthen fault tests to distinguish timeout, malformed JSON and HTTP status failures.

## Results

| Check | Result |
| --- | --- |
| JVM suite, including Java API | 31 passed |
| Android API 24, ARM64 emulator | 10 passed |
| Android API 36, ARM64 emulator | 10 passed |
| Release AAR build | Passed |
| SDK lint | 0 errors; 7 dependency-update advisories |
| Sample lint | No issues |
| Local Maven publication and sample consuming its AAR | Passed |
| Documentation build | 59 pages built |
| GitHub workflow actionlint | Passed |
| QA Python syntax and git diff checks | Passed |

Device tests cover real local Nona requests, frontend/backend scope enforcement,
separate-project caches, restore without a network request, ETag/304, release rollback,
prefix/pinned release, defaults, Java futures, sample buttons, malformed setup,
redirect rejection, response-size limits with and without Content-Length, and transport errors.

The original sample launch crash was reproduced on API 24 before the fix. The
original cross-origin redirect was reproduced on API 36. Redirect following was
classified as hardening, not a confirmed credential-exfiltration vulnerability:
attacker control of a trusted server redirect was not established and frontend keys
are documented as public.

An initial API 36 run encountered a transient SocketException in test-fixture admin
setup, before entering the SDK. A complete repeat passed all 10 tests. No automatic
retry was added to hide failures.

Earlier in the same change review, manual force-stop/offline/reset checks passed on
both emulators, and the unchanged JavaScript SDK/provider suites passed 50 tests.
These checks were not represented as newly rerun after the final Kotlin-only edits.

## Limits

The seven lint advisories concern newer AGP, Android test, coroutine and test JSON
versions; they are not runtime lint errors. A major build-tool migration was not
mixed into this fix. The wrapper JAR was inspected as an archive and hashed, but its
bytecode and upstream provenance were not independently audited. Actual Maven Central
publication, GitHub-hosted x86_64 emulator jobs and production deployment were not run.
No claim of an exhaustive dependency vulnerability audit is made.

The immutable Codex Security scan covered `be22343..134dbb9`; subsequent fixes were
reviewed and verified locally as described above. The scan's automatic inventory
excluded sample/test files; they were reviewed additionally.
