# Android SDK verification

Use a disposable local Nona database. The seed script creates the first administrator,
two independent projects, frontend/backend keys and two releases. It intentionally
refuses non-loopback servers. Never point these tests at production.

## Build and start the backend

Requires .NET 10, JDK 17, Python 3, Android SDK 36 and Android emulators.
From the repository root:

```sh
mkdir -p /tmp/nona-android-qa
dotnet build core/src/WebApi/WebApi.csproj -c Debug
Storage__Type=Sqlite \
Storage__Sqlite__DataSource=/tmp/nona-android-qa/nona.db \
ASPNETCORE_URLS=http://127.0.0.1:18686 \
dotnet run --project core/src/WebApi/WebApi.csproj --no-build --no-launch-profile
```

In separate terminals:

```sh
python3 client/kotlin/qa/seed-server.py --output /tmp/nona-android-qa/fixtures.json
python3 client/kotlin/qa/fault-server.py
```

Keep `fixtures.json` private and out of Git: it contains credentials for the
disposable server. Reseeding requires a new database.

## Unit tests, lint and device tests

```sh
cd client/kotlin
./gradlew :nona-client:testDebugUnitTest :nona-client:lintRelease :nona-client:assembleRelease \
  :sample:lintDebug :sample:assembleDebug :sample:assembleDebugAndroidTest
adb devices
python3 qa/run-device-tests.py --serial emulator-5554 \
  --fixtures /tmp/nona-android-qa/fixtures.json --output build/qa/device.txt
```

Run once per emulator, sequentially: rollback tests change the same disposable
release. The tests use `10.0.2.2` to reach the host, exercise the actual Android
HTTP/JSON/file implementations, and cover Java API, fetch/activate/304, prefix and
pinned releases, throttling, defaults on invalid values, cache identity, backend
scope rejection, invalid API keys, malformed responses, network timeouts, rejected
redirects, bounded response sizes and invalid sample setup. Fault endpoints use
loopback ports 18687 and 18688. The runner maps the fixture server to the emulator
host alias while preserving its port and path.

`kotlin-client.yml` runs the suite on API 24 and 36 for pull requests, using its own
ephemeral backend per matrix job. No repository secrets are needed.

## Manual process restart and offline test

Launch `com.nonaconfig.sample/.MainActivity` with intent extras `frontendKey` and
`baseUrl` (`http://10.0.2.2:18686`). Use the generated `android-qa-a` frontend key;
do not use the administrator token. The sample remembers its connection in app
preferences and does not fetch automatically.

1. Tap **Fetch**: the value stays at `default` until **Activate**.
2. Tap **Activate**: `flag` becomes `A`, source `REMOTE`, and invalid remote retries
   resolve to the in-app default `3`.
3. Force-stop the app and stop the disposable backend process.
4. Reopen the app: **Cache restored: true**, `flag: A`, and retries `3` must remain.
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
