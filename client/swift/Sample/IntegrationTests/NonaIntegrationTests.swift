import Foundation
import XCTest
import NonaClient

final class NonaIntegrationTests: XCTestCase {
    private func environment(_ name: String) throws -> String {
        try XCTUnwrap(ProcessInfo.processInfo.environment[name], "Run the integration suite using qa/run-simulator-tests.py; missing \(name)")
    }
    private func options(key: String? = nil, prefix: String? = nil,
                         useReleases: Bool = false, version: String? = nil) throws -> NonaOptions {
        // These tests check backend behavior, not latency. Allow for the first
        // request and scheduling delays on shared CI runners. Fault-server tests
        // below keep short timeouts only where timeout handling is under test.
        try NonaOptions(baseURL: URL(string: environment("NONA_BASE_URL"))!, environmentID: "Production",
                        apiKey: key ?? environment("NONA_FRONTEND_A"), useReleases: useReleases,
                        releaseVersion: version, prefix: prefix,
                        minimumFetchInterval: 0, requestTimeout: 15)
    }

    func testRealServerFetch304DefaultsAndRestore() async throws {
        let store = InMemorySnapshotStore()
        let config = NonaConfig(options: try options(), store: store)
        config.setDefaults(["flag": "default", "Limits:Retries": 3])
        let status = try await config.fetch()
        XCTAssertEqual(status, .success)
        XCTAssertEqual(config.getString("flag"), "default")
        XCTAssertTrue(config.activate())
        XCTAssertEqual(config.getString("flag"), "A-new")
        XCTAssertEqual(config.getLong("Limits:Retries"), 3)
        XCTAssertFalse(config.keys.contains("Hidden"))
        let unchanged = try await config.fetch()
        XCTAssertEqual(unchanged, .notModified)
        let restored = NonaConfig(options: try options(), store: store)
        let loaded = try await restored.initialize()
        XCTAssertTrue(loaded)
        XCTAssertEqual(restored.getString("flag"), "A-new")
    }

    func testRealServerProjectIsolationAndSelectors() async throws {
        let a = NonaConfig(options: try options(), store: InMemorySnapshotStore())
        let b = NonaConfig(options: try options(key: environment("NONA_FRONTEND_B")), store: InMemorySnapshotStore())
        try await a.fetchAndActivate(); try await b.fetchAndActivate()
        XCTAssertEqual(a.getString("flag"), "A-new")
        XCTAssertEqual(b.getString("flag"), "B")
        let activeRelease = NonaConfig(options: try options(useReleases: true), store: InMemorySnapshotStore())
        try await activeRelease.fetchAndActivate()
        XCTAssertEqual(activeRelease.getString("flag"), "A")
        let pinned = NonaConfig(options: try options(prefix: "Features:", useReleases: true, version: "1.0.0"),
                                store: InMemorySnapshotStore())
        try await pinned.fetchAndActivate()
        XCTAssertEqual(pinned.keys, ["Features:Checkout"])
        XCTAssertTrue(pinned.getBoolean("Features:Checkout"))
    }

    func testRealServerRejectsWrongKeyScopes() async throws {
        let cases: [(key: String, statusCode: Int, errorCode: String)] = [
            (try environment("NONA_BACKEND_KEY"), 404, "environment_not_found"),
            (String(repeating: "A", count: 64), 401, "invalid_api_key"),
        ]
        for item in cases {
            let config = NonaConfig(options: try options(key: item.key), store: InMemorySnapshotStore())
            do { try await config.fetch(); XCTFail("Expected HTTP failure") }
            catch let error as NonaError {
                XCTAssertEqual(error.statusCode, item.statusCode)
                XCTAssertEqual(error.errorCode, item.errorCode)
            }
        }
    }

    func testDefaultTransportRejectsRedirectsOversizeMalformedAndTimeout() async throws {
        let base = try environment("NONA_FAULT_URL")
        for (route, expected) in [("redirect", NonaError.http(statusCode: 302)),
                                  ("large", .responseTooLarge(maxBytes: 64)),
                                  ("large-no-length", .responseTooLarge(maxBytes: 64)),
                                  ("malformed", .invalidSnapshot), ("http503", .http(statusCode: 503)),
                                  ("slow", .transport)] {
            let options = try NonaOptions(baseURL: URL(string: "\(base)/\(route)")!, environmentID: "Production",
                                          apiKey: "test-only-key", requestTimeout: route == "slow" ? 0.1 : 15,
                                          maxResponseBytes: 64)
            let config = NonaConfig(options: options, store: InMemorySnapshotStore())
            config.setDefaults(["flag": "fallback"])
            do { try await config.fetch(); XCTFail("Expected \(route) failure") }
            catch let error as NonaError { XCTAssertEqual(error, expected, route) }
            XCTAssertEqual(config.getString("flag"), "fallback")
            XCTAssertFalse(config.activate())
        }
    }
    func testInterruptedBodiesCannotReplaceActiveCache() async throws {
        struct SeedHTTP: NonaHTTPClient {
            func get(url: URL, headers: [String: String]) async throws -> NonaHTTPResponse {
                NonaHTTPResponse(statusCode: 200, body: Data(#"{"flag":{"value":"old"}}"#.utf8))
            }
        }
        let base = try environment("NONA_FAULT_URL")
        for route in ["short-body", "partial-json", "disconnect", "trickle"] {
            let opts = try NonaOptions(baseURL: URL(string: "\(base)/\(route)")!, environmentID: "Production",
                                       minimumFetchInterval: 0, requestTimeout: 3, maxResponseBytes: 64)
            let store = InMemorySnapshotStore()
            let seed = NonaConfig(options: opts, store: store, http: SeedHTTP())
            try await seed.fetch()
            let original = store.read()
            let config = NonaConfig(options: opts, store: store)
            let restored = try await config.initialize()
            XCTAssertTrue(restored)
            do { try await config.fetch(); XCTFail("Expected failure: \(route)") }
            catch is NonaError {} catch { XCTFail("Unexpected error: \(error)") }
            XCTAssertEqual(config.getString("flag"), "old", route)
            XCTAssertFalse(config.activate(), route)
            XCTAssertEqual(store.read(), original, route)
        }
    }

}
