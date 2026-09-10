# Mobile SDK failure and compatibility checks — 2026-09-06

This follow-up tests the Swift and Kotlin working trees on `feature/swift-client`.
The earlier Kotlin architecture fixes remain part of that working tree. No commit,
release tag, registry publication or production backend change was made here.

## First-release decision

The owner confirmed that nobody currently uses the Kotlin SDK. `Flow` is therefore
the intended API for the first release: no external consumer migration is needed,
and the difference from the pre-release SharedFlow API does not block publication.
The comparison below remains a factual record of that API change, not an open
migration task. No server or cache migration is required.

## New findings and fixes

1. **Different accepted values across platforms.** Kotlin accepted a numeric
   `contentType` and treated it as text, whereas Swift rejected it. Kotlin parsed
   `1.0f` as a number; Swift accepted hexadecimal floating-point syntax. Both now
   use decimal numeric syntax, with optional sign, fraction and exponent, and
   finite results. Content type must be a string, omitted or null; an unknown
   string content type is preserved. Kotlin also rejects trailing data after the
   JSON object. These are intentional parsing behavior changes to document in
   release notes; the corpus does not claim complete RFC JSON fuzz coverage.
2. **Android-only cache coercion.** System `JSONObject.getString` converted a
   cached number into a string. The equivalent JVM test passed because its JSON
   implementation was stricter. The device regression failed before correction.
   Cache entries now use explicit wire-format validation, and cache timestamp/ETag
   types are checked before restoring state.
3. **Known Kotlin binary incompatibility confirmed.** Comparing the compiled
   baseline and current JVM signatures reports the removal of the old
   `getConfigUpdates(): SharedFlow` descriptor. The replacement returns `Flow`.
   Cache compatibility and source consumer builds do not erase this incompatibility.
   The API check exits 1 deliberately: migrate/rebuild consumers and select a new
   version before release. This is not reported as a passing compatibility gate.

## Repeatable coverage

`contracts/snapshots.json` is the single source for 16 snapshot cases, 16 typed
value cases, and real cache samples produced by baseline commit `6782f7c`.
`generate-contracts.py` embeds it in JVM and Swift test sources. Android device
tests read the same JSON from instrumentation-only assets. CI runs `--check` to
prevent stale generated sources. Fixtures and probes are not SDK runtime dependencies.

The corpus covers empty snapshots, Unicode, omitted/null/unknown content types,
wrong root/entry/value types, truncated JSON and trailing data; numeric boundaries,
overflow, exponent/fraction syntax, non-finite values, language-specific suffixes,
non-ASCII digits and boolean parsing. Historical cache tests restore without HTTP.

Additional tests cover failed filesystem writes and subsequent recovery,
cancellation of reset while a committed write is blocked, two file-store instances
writing atomically to one path, corrupt cached value types, repeated Swift subscriber
cancellation, and a failing Kotlin subscriber leaving another subscriber operational.
The tests establish file integrity, not a transactional reset across client instances.

The local fault server adds a complete-looking body shorter than Content-Length,
truncated JSON, disconnection before headers, and slow byte-by-byte streaming.
Native transport tests verify none can replace the active snapshot or its cache.
Connect/read timeouts remain distinct from a total request deadline in Kotlin.

`check-upgrade.py` builds the actual baseline and current sources in isolated
locations. Both SDKs successfully read each other's persisted cache. The Swift
consumer links through SwiftPM using only public API. Its process is then killed
while repeatedly saving 256 KiB snapshots; the remaining cache must be a complete
old or new value. The Android manual probe force-stops its own emulator app during
repeated real SDK fetch/persist operations and verifies the private cache afterwards.
These runs sample abrupt termination; they do not force a kill at every instruction
or prove durability through physical power loss.

## Results

| Check | Result |
| --- | --- |
| Kotlin JVM suite | 46 passed, zero failed/skipped |
| Android API 24 ARM64 | 14 passed |
| Android API 37 ARM64 | 14 passed |
| Swift macOS with Thread Sanitizer and strict concurrency | 33 passed; no sanitizer diagnostics |
| Swift 6 compilation with warnings as errors | Passed |
| iOS 26.5 simulator | 40 passed, zero failed/skipped |
| Cache upgrade and downgrade against actual baseline builds | Both SDKs passed |
| Swift and Android process-kill probes | Passed |
| Public SwiftPM consumer build | Baseline and current passed |
| CocoaPods lint, iOS/macOS, Swift 5.9 | Passed; local toolchain notes remain |
| Local Maven AAR publication and consumer Release build | Passed |
| Shared Python suite / generated corpus / actionlint | 8 tests passed; checks passed |
| Selected JVM public-signature compatibility gate | **Fails as expected: SharedFlow → Flow** |

## Running the checks

From the repository root, with the SDK build prerequisites installed:

```sh
python3 client/qa/generate-contracts.py --check
python3 -m unittest discover -s client/qa -p 'test_*.py'
swift test --sanitize thread -Xswiftc -strict-concurrency=complete -Xswiftc -warnings-as-errors
./client/kotlin/gradlew -p client/kotlin :nona-client:testDebugUnitTest
python3 client/qa/check-upgrade.py --baseline-ref 6782f7c --output /tmp/nona-upgrade-NEW
```

The upgrade output directory must be new. It contains the baseline archive,
consumer builds, logs and summary; choose an explicit reviewed baseline commit.
It does not modify git branches or publish packages. Requires macOS/Swift, JDK 17,
Android SDK and the Kotlin build dependencies.

Run native suites sequentially against the disposable backend using the platform
QA runners. After installing the current debug and test APKs, the opt-in Android
kill probe is:

```sh
python3 client/kotlin/qa/check-process-kill.py --serial emulator-5556 \
  --fixtures /tmp/nona-sdk-qa/fixtures.json --output /tmp/nona-android-kill.log
```

It requires an emulator, force-stops only `com.nonaconfig.sample`, and uses its own
`files/process-kill-probe` directory. The normal QA runner excludes `ManualProbe`;
without its explicit argument the probe is skipped, not run indefinitely.

The API gate takes compiled baseline/current jars:

```sh
python3 client/qa/check-kotlin-api.py --baseline-jar /path/to/baseline/classes.jar \
  --current-jar /path/to/current/classes.jar
```

It checks selected public entry-point descriptors, not Kotlin metadata, all possible
Java reflection usage or a comprehensive API compatibility model. Exit 1 requires
review and a version/migration decision; do not hide it in a successful test count.

## Remaining boundaries

- Physical-device lock/unlock, background execution, memory pressure, OS backup
  and restore, and actual disk exhaustion were not simulated. Filesystem failure
  tests use an unwritable path shape; they do not fill the user's disk.
- TLS certificate-expiry/hostname/trust-chain combinations and interactions with
  application-installed global networking handlers need separate integration tests.
- Persistent cache is best effort: an I/O failure can prevent reset from removing
  an old file. It is not secure erasure or an authorization boundary.
- Multiple clients/processes sharing one path have atomic file replacement, not
  globally ordered reset semantics. Prefer one client per configuration identity.
- Long-running heap profiling and exhaustive scheduling/fuzz campaigns were not
  performed. Lifecycle tests check specific cancellation/isolation behavior.
- React Native/npm and public registry installation were outside these mobile-native
  source checks. The baseline is a local commit, not a claimed published release.

Evidence remains local under `/tmp/nona-risk-*`, especially `nona-risk-upgrade/`,
`nona-risk-api24-final.txt`, `nona-risk-api37-final.txt`, `nona-risk-swift-final.log`
and `nona-risk-api.log`. Do not treat these temporary paths as portable CI artifacts.
