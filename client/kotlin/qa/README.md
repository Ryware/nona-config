# Android SDK verification

See the [architecture audit](ARCHITECTURE.md) for corrections, concurrency
contracts and the `configUpdates` API migration.

Start and seed a disposable backend using the [shared SDK QA setup](../../qa/README.md).
Then follow the Android-specific steps below. Requires JDK 17, Android SDK 36
and an Android emulator in addition to the shared prerequisites.

## Unit tests, lint and device tests

```sh
cd client/kotlin
./gradlew :nona-client:testDebugUnitTest :nona-client:lintRelease :nona-client:assembleRelease \
  :sample:lintDebug :sample:assembleDebug :sample:assembleDebugAndroidTest
adb devices
python3 qa/run-device-tests.py --serial emulator-5554 \
  --fixtures /tmp/nona-sdk-qa/fixtures.json --output build/qa/device.txt
```

Run once per emulator, sequentially: rollback tests change the same disposable
release. The runner configures `adb reverse` for the selected emulator and uses
`127.0.0.1` to reach the host. Tests exercise the actual Android
HTTP/JSON/file implementations, and cover Java API, fetch/activate/304, prefix and
pinned releases, throttling, defaults on invalid values, cache identity, backend
scope rejection, invalid API keys, malformed responses, network timeouts, rejected
redirects, bounded response sizes and invalid sample setup. Fault endpoints use
loopback ports 18687 and 18688. The runner preserves the fixture server port and path, forwards both fault
ports, reuses matching reverse mappings and refuses to replace conflicting ones.
Mappings remain available until the emulator or ADB connection closes. Failed
suites also save `.network.txt` diagnostics beside the test output.

`kotlin-client.yml` runs the suite on API 24 and 36 for pull requests, using its own
ephemeral backend per matrix job. No repository secrets are needed.

## Manual process restart and offline test

Launch `com.nonaconfig.sample/.MainActivity` with intent extras `frontendKey` and
`baseUrl` (`http://10.0.2.2:18686`). Use the generated `sdk-qa-a` frontend key;
do not use the administrator token. The sample remembers its connection in app
preferences and does not fetch automatically.

1. Tap **Fetch**: the value stays at `default` until **Activate**.
2. Tap **Activate**: `flag` becomes `A-new`, source `REMOTE`, and invalid remote retries
   resolve to the in-app default `3`.
3. Force-stop the app and stop the disposable backend process.
4. Reopen the app: **Cache restored: true**, `flag: A-new`, and retries `3` must remain.
5. Tap **Fetch and activate**: failure is displayed without a crash or loss of the
   cached value. Check the app's crash log and save a screenshot.
6. Tap **Reset**, force-stop and reopen: defaults must remain, with no old snapshot.

Cleartext networking is enabled only in the sample's debug manifest. The library
and release sample preserve the consuming application's HTTPS policy.

## Verify Maven consumption without publishing

```sh
./gradlew :nona-client:publishToMavenLocal -Dmaven.repo.local=/tmp/nona-android-maven
./gradlew -I qa/consume-published.init.gradle -Dnona.qa.repository=/tmp/nona-android-maven \
  :sample:assembleDebug :sample:assembleDebugAndroidTest
```

The second build substitutes the local Maven artifact for the project dependency,
checking the AAR and its transitive dependency metadata. It does not upload anything.
