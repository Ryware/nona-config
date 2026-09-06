# Swift SDK QA

See the [architecture audit](ARCHITECTURE.md) for invariants, corrections,
current validation results and remaining release checks.

Start and seed a disposable backend using the [shared SDK QA setup](../../qa/README.md).
It provides two projects, frontend/backend keys, versioned releases and fault endpoints.
The iOS simulator reaches the host through `127.0.0.1`.

From the repository root:

```sh
swift test -Xswiftc -strict-concurrency=complete -Xswiftc -warnings-as-errors
pod install --project-directory=client/swift/Sample
xcrun simctl list devices available
python3 client/swift/qa/run-simulator-tests.py \
  --simulator YOUR_SIMULATOR_UDID \
  --fixtures /tmp/nona-sdk-qa/fixtures.json \
  --result-bundle /tmp/nona-swift-tests.xcresult
```

Choose a new result-bundle path for each run. Simulator integration tests require
the environment passed by this script and fail clearly if fixtures are absent.
They check real HTTP/304, key scopes, project isolation, pinned/prefixed values,
malformed/error/timeout responses, redirects, and bounded response reads. Unit tests
add reset/cancellation races, file caching, type fallbacks and update subscribers.

For manual verification, run either sample scheme, paste the frontend key and
server URL, then Connect & restore → Fetch → Activate. The active flag should stay
`default` until activation, then become `A`; retries remain `3` because the fixture's
remote value is malformed. Relaunch to restore the cache. Stop the disposable
backend to verify a failed fetch preserves values. Reset should clear the cache.

Never run these tests against production. Result bundles/logs may contain disposable
fixture context, so do not publish them as unrestricted CI artifacts.
