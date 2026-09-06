# Shared SDK QA backend

These Python scripts provide disposable server fixtures and HTTP fault endpoints
for Swift and Kotlin SDK tests. They are development tools, not SDK dependencies.
Requires .NET 10 and Python 3. Never use a production database.

From the repository root, start a fresh backend:

```sh
mkdir -p /tmp/nona-sdk-qa
dotnet build core/src/WebApi/WebApi.csproj -c Debug
Storage__Type=Sqlite \
Storage__Sqlite__DataSource=/tmp/nona-sdk-qa/nona.db \
ASPNETCORE_URLS=http://127.0.0.1:18686 \
dotnet run --project core/src/WebApi/WebApi.csproj --no-build --no-launch-profile
```

Once the server is listening, seed it in another terminal:

```sh
python3 client/qa/seed-server.py --output /tmp/nona-sdk-qa/fixtures.json
python3 client/qa/fault-server.py
```

The seed creates an administrator, projects `sdk-qa-a` and `sdk-qa-b`, frontend
and backend keys, and versioned releases. Keep `fixtures.json` private and out of
Git. Reseeding requires a fresh database. After upgrading from the old Android QA
setup, recreate the database and fixtures: project names and fixture keys changed.

Fault endpoints listen on loopback ports 18687 and 18688 and exercise malformed
JSON, HTTP errors, timeouts, redirects and response-size limits. Swift connects
through `127.0.0.1`; the Android runner maps the backend URL to `10.0.2.2`.
Redirect targets preserve the requesting simulator's loopback/host alias, so they
are reachable on either platform. Both SDKs must reject redirects before contacting
the target. Other Host values fall back to loopback.

Continue with [Swift simulator QA](../swift/qa/README.md) or
[Android device QA](../kotlin/qa/README.md). Run suites sequentially against a shared
database because release rollback tests change the active release. CI uses an
isolated database for each job.

Run shared fixture and runner-contract tests with:

```sh
python3 -m unittest discover -s client/qa -p 'test_*.py'
```
