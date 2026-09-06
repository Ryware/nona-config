import Foundation
import XCTest
import NonaClient

final class NonaIntegrationTests: XCTestCase {
    private func environment(_ name: String) throws -> String {
        try XCTUnwrap(ProcessInfo.processInfo.environment[name], "Run the integration suite using qa/run-simulator-tests.py; missing \(name)")
    }
    private func options(key: String? = nil, prefix: String? = nil, version: String? = nil) throws -> NonaOptions {
        try NonaOptions(baseURL: URL(string: environment("NONA_BASE_URL"))!, environmentID: "Production",
                        apiKey: key ?? environment("NONA_FRONTEND_A"), releaseVersion: version, prefix: prefix,
                        minimumFetchInterval: 0, requestTimeout: 3)
    }

    func testRealServerFetch304DefaultsAndRestore() async throws {
        let store = InMemorySnapshotStore()
        let config = NonaConfig(options: try options(), store: store)
        config.setDefaults(["flag": "default", "Limits:Retries": 3])
        let status = try await config.fetch()
        XCTAssertEqual(status, .success)
        XCTAssertEqual(config.getString("flag"), "default")
        XCTAssertTrue(config.activate())
        XCTAssertEqual(config.getString("flag"), "A")
        XCTAssertEqual(config.getLong("Limits:Retries"), 3)
        XCTAssertFalse(config.keys.contains("Hidden"))
        let unchanged = try await config.fetch()
        XCTAssertEqual(unchanged, .notModified)
        let restored = NonaConfig(options: try options(), store: store)
        let loaded = try await restored.initialize()
        XCTAssertTrue(loaded)
        XCTAssertEqual(restored.getString("flag"), "A")
    }

    func testRealServerProjectIsolationAndSelectors() async throws {
        let a = NonaConfig(options: try options(), store: InMemorySnapshotStore())
        let b = NonaConfig(options: try options(key: environment("NONA_FRONTEND_B")), store: InMemorySnapshotStore())
        try await a.fetchAndActivate(); try await b.fetchAndActivate()
        XCTAssertEqual(a.getString("flag"), "A")
        XCTAssertEqual(b.getString("flag"), "B")
        let pinned = NonaConfig(options: try options(prefix: "Features:", version: "1.0.0"), store: InMemorySnapshotStore())
        try await pinned.fetchAndActivate()
        XCTAssertEqual(pinned.keys, ["Features:Checkout"])
        XCTAssertTrue(pinned.getBoolean("Features:Checkout"))
    }

    func testRealServerRejectsWrongKeyScopes() async throws {
        for (key, code) in [(try environment("NONA_BACKEND_KEY"), 404), (String(repeating: "A", count: 64), 401)] {
            let config = NonaConfig(options: try options(key: key), store: InMemorySnapshotStore())
            do { try await config.fetch(); XCTFail("Expected HTTP failure") }
            catch let error as NonaError { XCTAssertEqual(error, .http(statusCode: code)) }
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
                                          apiKey: "test-only-key", requestTimeout: route == "slow" ? 0.1 : 3,
                                          maxResponseBytes: 64)
            let config = NonaConfig(options: options, store: InMemorySnapshotStore())
            config.setDefaults(["flag": "fallback"])
            do { try await config.fetch(); XCTFail("Expected \(route) failure") }
            catch let error as NonaError { XCTAssertEqual(error, expected, route) }
            XCTAssertEqual(config.getString("flag"), "fallback")
            XCTAssertFalse(config.activate())
        }
    }
}
