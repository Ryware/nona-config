import Foundation
import XCTest
@testable import NonaClient

private struct StubHTTP: NonaHTTPClient {
    let handler: @Sendable (URL, [String: String]) async throws -> NonaHTTPResponse
    func get(url: URL, headers: [String: String]) async throws -> NonaHTTPResponse { try await handler(url, headers) }
}

private func body(_ value: String = "A") -> Data {
    try! JSONEncoder().encode(["flag": NonaEntry(value: value)])
}

private func problem(status: Int, code: String, detail: String) -> Data {
    try! JSONSerialization.data(withJSONObject: [
        "title": "Request failed",
        "status": status,
        "detail": detail,
        "errorCode": code
    ])
}

private actor Latch {
    private var open = false
    private var waiters: [CheckedContinuation<Void, Never>] = []
    func wait() async { if !open { await withCheckedContinuation { waiters.append($0) } } }
    func release() { open = true; waiters.forEach { $0.resume() }; waiters.removeAll() }
}

private final class BlockingStore: NonaSnapshotStore {
    let entered = XCTestExpectation(description: "Cache operation entered")
    let resume = DispatchSemaphore(value: 0)
    let operation = Locked<String?>(nil)
    private let data = InMemorySnapshotStore()
    func read() -> Data? { pause("read"); return data.read() }
    func write(_ value: Data) { pause("write"); data.write(value) }
    func clear() { pause("clear"); data.clear() }
    private func pause(_ name: String) {
        let shouldPause = operation.withLock { value -> Bool in
            guard value == name else { return false }
            value = nil
            return true
        }
        if shouldPause {
            entered.fulfill()
            _ = resume.wait(timeout: .now() + 10)
        }
    }
}

final class NonaConfigTests: XCTestCase {
    private func options(key: String = "frontend", interval: TimeInterval = 0) throws -> NonaOptions {
        try NonaOptions(baseURL: URL(string: "https://nona.test")!, environmentID: "Production", apiKey: key,
                        minimumFetchInterval: interval)
    }
    private func client(_ value: String = "A", store: any NonaSnapshotStore = InMemorySnapshotStore()) throws -> NonaConfig {
        NonaConfig(options: try options(), store: store, http: StubHTTP { _, _ in
            NonaHTTPResponse(statusCode: 200, body: body(value), etag: "a")
        })
    }

    func testFetchRequiresActivation() async throws {
        let config = try client()
        config.setDefaults(["flag": "default"])
        let status = try await config.fetch()
        XCTAssertEqual(status, .success)
        XCTAssertEqual(config.getString("flag"), "default")
        XCTAssertTrue(config.activate())
        XCTAssertEqual(config.getString("flag"), "A")
        XCTAssertEqual(config.getSource("flag"), .remote)
        XCTAssertFalse(config.activate())
    }

    func testSynchronousReadsDoNotWaitForCacheIO() async throws {
        for operation in ["read", "write", "clear"] {
            let store = BlockingStore()
            let seed = try client(store: store)
            try await seed.fetch()
            let config = try client(store: store)
            config.setDefaults(["local": "ready"])
            store.operation.withLock { $0 = operation }
            let work = Task {
                switch operation {
                case "read": _ = try await config.initialize()
                case "write": _ = try await config.fetch()
                default: try await config.reset()
                }
            }
            await fulfillment(of: [store.entered], timeout: 5)
            let read = expectation(description: "Read while \(operation) is blocked")
            let reader = Task.detached {
                XCTAssertEqual(config.getString("local"), "ready")
                read.fulfill()
            }
            await fulfillment(of: [read], timeout: 1)
            store.resume.signal()
            await reader.value
            try await work.value
        }
    }

    func testCancelledInitializationDoesNotRestoreCache() async throws {
        let store = BlockingStore()
        let seed = try client(store: store)
        try await seed.fetch()
        let config = try client(store: store)
        store.operation.withLock { $0 = "read" }
        let work = Task { try await config.initialize() }
        await fulfillment(of: [store.entered], timeout: 5)
        work.cancel()
        store.resume.signal()
        do { _ = try await work.value; XCTFail("Expected cancellation") }
        catch is CancellationError {} catch { XCTFail("\(error)") }
        XCTAssertEqual(config.getSource("flag"), .static)
    }

    func testTypedDefaultsAndResolution() async throws {
        let config = try client("bad")
        config.setDefaults(["flag": 42, "boolean": true, "decimal": 2.5, "text": "hello"])
        try await config.fetchAndActivate()
        XCTAssertEqual(config.getLong("flag"), 42)
        XCTAssertEqual(config.getDouble("decimal"), 2.5)
        XCTAssertTrue(config.getBoolean("boolean"))
        XCTAssertEqual(config.keys, ["flag", "boolean", "decimal", "text"])
        if case .failure(let reason, _) = config.resolveLong("flag") { XCTAssertEqual(reason, .typeMismatch) }
        else { XCTFail("Expected type mismatch") }
        XCTAssertEqual(config.getString("absent"), "")
        XCTAssertEqual(config.getSource("absent"), .static)
    }

    func testFiniteNumbersAndBooleanParsing() async throws {
        for invalid in ["NaN", "Infinity", "1e1000", ""] {
            let config = try client(invalid)
            config.setDefaults(["flag": 7.5])
            try await config.fetchAndActivate()
            XCTAssertEqual(config.getDouble("flag"), 7.5)
        }
        let config = try client("  TRUE \n")
        try await config.fetchAndActivate()
        XCTAssertTrue(config.getBoolean("flag"))
        XCTAssertEqual(config.getLong("flag"), 0)
    }

    func testNotReadyAndMissingAreDistinct() async throws {
        let config = try client()
        if case .failure(let reason, _) = config.resolveString("missing") { XCTAssertEqual(reason, .notReady) }
        else { XCTFail() }
        try await config.fetchAndActivate()
        if case .failure(let reason, _) = config.resolveString("missing") { XCTAssertEqual(reason, .notFound) }
        else { XCTFail() }
    }

    func testCacheSurvivesRestartButDoesNotCrossProject() async throws {
        let store = InMemorySnapshotStore()
        let first = try client(store: store)
        try await first.fetchAndActivate()
        let same = try client(store: store)
        let restored = try await same.initialize()
        XCTAssertTrue(restored)
        XCTAssertEqual(same.getString("flag"), "A")
        let other = NonaConfig(options: try options(key: "other"), store: store)
        let wrong = try await other.initialize()
        XCTAssertFalse(wrong)
        XCTAssertEqual(other.getString("flag"), "")
    }

    func testCorruptAndLegacyCacheAreIgnored() async throws {
        for data in [Data("invalid".utf8), Data("{}".utf8)] {
            let store = InMemorySnapshotStore()
            store.write(data)
            let config = try client(store: store)
            let restored = try await config.initialize()
            XCTAssertFalse(restored)
        }
    }

    func testFileCacheAtomicReplacementAndReset() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let store = FileSnapshotStore(fileURL: directory.appendingPathComponent("snapshot.json"))
        let config = try client(store: store)
        try await config.fetchAndActivate()
        let second = try client("B", store: store)
        try await second.fetchAndActivate()
        let restored = try client(store: store)
        let loaded = try await restored.initialize()
        XCTAssertTrue(loaded)
        XCTAssertEqual(restored.getString("flag"), "B")
        try await restored.reset()
        XCTAssertNil(store.read())
        XCTAssertFalse(restored.activate())
        XCTAssertEqual(restored.getString("flag"), "")
    }

    func testRollbackUsesPendingETag() async throws {
        let current = Locked("A")
        let config = NonaConfig(options: try options(), store: InMemorySnapshotStore(), http: StubHTTP { _, headers in
            let version = current.withLock { $0 }
            return headers["If-None-Match"] == version ? NonaHTTPResponse(statusCode: 304)
                : NonaHTTPResponse(statusCode: 200, body: body(version), etag: version)
        })
        try await config.fetchAndActivate()
        current.withLock { $0 = "B" }
        try await config.fetch()
        current.withLock { $0 = "A" }
        let changed = try await config.fetchAndActivate()
        XCTAssertFalse(changed)
        XCTAssertEqual(config.getString("flag"), "A")
    }

    func test304RevalidatesPendingAndPersistsThrottleTime() async throws {
        let store = InMemorySnapshotStore()
        let now = Locked(1000.0)
        let calls = Locked(0)
        let opts = try options(interval: 60)
        let http = StubHTTP { _, headers in
            calls.withLock { $0 += 1 }
            return headers["If-None-Match"] == nil
                ? NonaHTTPResponse(statusCode: 200, body: body(), etag: "a") : NonaHTTPResponse(statusCode: 304)
        }
        let config = NonaConfig(options: opts, store: store, http: http, clock: { now.withLock { $0 } })
        try await config.fetch()
        now.withLock { $0 += 61 }
        let status = try await config.fetch()
        XCTAssertEqual(status, .notModified)
        XCTAssertEqual(config.getString("flag"), "")
        XCTAssertTrue(config.activate())
        let restored = NonaConfig(options: opts, store: store, http: http, clock: { now.withLock { $0 } })
        try await restored.initialize()
        let throttled = try await restored.fetch()
        XCTAssertEqual(throttled, .throttled)
        XCTAssertEqual(calls.withLock { $0 }, 2)
    }

    func testUnexpected304AndMalformedResponsesDoNotThrottle() async throws {
        for response in [NonaHTTPResponse(statusCode: 304), NonaHTTPResponse(statusCode: 200, body: Data("{\"flag\":{\"value\":123}}".utf8)), NonaHTTPResponse(statusCode: 503)] {
            let calls = Locked(0)
            let config = NonaConfig(options: try options(interval: 60), store: InMemorySnapshotStore(), http: StubHTTP { _, _ in
                calls.withLock { $0 += 1 }; return response
            })
            for _ in 0..<2 {
                do { try await config.fetch(); XCTFail("Expected failure") } catch is NonaError {} catch { XCTFail("\(error)") }
            }
            XCTAssertEqual(calls.withLock { $0 }, 2)
            XCTAssertFalse(config.activate())
        }
    }

    func testFailedRefreshPreservesActiveSnapshotAndAllowsRetry() async throws {
        let response = Locked(NonaHTTPResponse(statusCode: 200, body: body("good"), etag: "good"))
        let config = NonaConfig(options: try options(interval: 60), store: InMemorySnapshotStore(),
                                http: StubHTTP { _, _ in response.withLock { $0 } })
        try await config.fetchAndActivate()
        for bad in [NonaHTTPResponse(statusCode: 503),
                    NonaHTTPResponse(statusCode: 200, body: Data("[]".utf8))] {
            response.withLock { $0 = bad }
            do { try await config.fetch(minimumFetchInterval: 0); XCTFail("Expected failure") }
            catch is NonaError {} catch { XCTFail("\(error)") }
            XCTAssertEqual(config.getString("flag"), "good")
            XCTAssertFalse(config.activate())
        }
        response.withLock { $0 = NonaHTTPResponse(statusCode: 200, body: body("recovered")) }
        try await config.fetchAndActivate(minimumFetchInterval: 0)
        XCTAssertEqual(config.getString("flag"), "recovered")
    }

    func testReleaseRefreshFailurePreservesSnapshotSourceAndETag() async throws {
        let calls = Locked(0)
        let urls = Locked<[URL]>([])
        let requestHeaders = Locked<[[String: String]]>([])
        let opts = try NonaOptions(baseURL: URL(string: "https://nona.test")!, environmentID: "Production",
                                   useReleases: true, releaseVersion: "2.x", minimumFetchInterval: 0)
        let config = NonaConfig(options: opts, store: InMemorySnapshotStore(), http: StubHTTP { url, headers in
            urls.withLock { $0.append(url) }
            requestHeaders.withLock { $0.append(headers) }
            return calls.withLock { count in
                defer { count += 1 }
                if count == 0 { return NonaHTTPResponse(statusCode: 200, body: body("released"), etag: "release-etag") }
                return NonaHTTPResponse(statusCode: 409,
                                        body: problem(status: 409, code: "active_release_not_configured",
                                                      detail: "The wording may change"))
            }
        })

        try await config.fetchAndActivate()
        do { try await config.fetch(); XCTFail("Expected release refresh failure") }
        catch let error as NonaError {
            XCTAssertEqual(error.statusCode, 409)
            XCTAssertEqual(error.errorCode, "active_release_not_configured")
            XCTAssertEqual(error.detail, "The wording may change")
        }
        XCTAssertEqual(config.getString("flag"), "released")
        XCTAssertFalse(config.activate())
        XCTAssertEqual(Set(urls.withLock { $0.map(\.path) }), ["/api/Production/releases/parameters"])
        XCTAssertEqual(requestHeaders.withLock { $0.last?["If-None-Match"] }, "release-etag")
    }

    func testCustomTransportStillRespectsResponseLimit() async throws {
        let opts = try NonaOptions(baseURL: URL(string: "https://nona.test")!, environmentID: "Production",
                                   maxResponseBytes: 16)
        let config = NonaConfig(options: opts, store: InMemorySnapshotStore(),
                                http: StubHTTP { _, _ in NonaHTTPResponse(statusCode: 200, body: body()) })
        do { try await config.fetch(); XCTFail("Expected size limit") }
        catch let error as NonaError { XCTAssertEqual(error, .responseTooLarge(maxBytes: 16)) }
        XCTAssertFalse(config.activate())
    }

    func testResetDiscardsInFlightFetch() async throws {
        let entered = Latch(), release = Latch()
        let store = InMemorySnapshotStore()
        let config = NonaConfig(options: try options(), store: store, http: StubHTTP { _, _ in
            await entered.release(); await release.wait()
            return NonaHTTPResponse(statusCode: 200, body: body())
        })
        let task = Task { try await config.fetch() }
        await entered.wait()
        try await config.reset()
        await release.release()
        let result = try await task.value
        XCTAssertEqual(result, .discarded)
        XCTAssertNil(store.read())
        XCTAssertFalse(config.activate())
    }

    func testCancelledFetchDoesNotPersistOrActivate() async throws {
        let entered = Latch(), release = Latch()
        let store = InMemorySnapshotStore()
        let config = NonaConfig(options: try options(), store: store, http: StubHTTP { _, _ in
            await entered.release(); await release.wait()
            return NonaHTTPResponse(statusCode: 200, body: body())
        })
        let task = Task { try await config.fetch() }
        await entered.wait(); task.cancel(); await release.release()
        do { _ = try await task.value; XCTFail("Expected cancellation") } catch is CancellationError {} catch { XCTFail() }
        XCTAssertNil(store.read())
        XCTAssertFalse(config.activate())
    }

    func testConcurrentFetchesSerializeAndCancelledWaitersDoNotRun() async throws {
        let entered = Latch(), release = Latch(), calls = Locked(0)
        let config = NonaConfig(options: try options(interval: 60), store: InMemorySnapshotStore(), http: StubHTTP { _, _ in
            calls.withLock { $0 += 1 }
            await entered.release(); await release.wait()
            return NonaHTTPResponse(statusCode: 200, body: body())
        })
        let first = Task { try await config.fetch() }
        await entered.wait()
        let second = Task { try await config.fetch() }
        second.cancel()
        do { _ = try await second.value; XCTFail() } catch is CancellationError {} catch { XCTFail() }
        await release.release()
        _ = try await first.value
        let status = try await config.fetch()
        XCTAssertEqual(status, .throttled)
        XCTAssertEqual(calls.withLock { $0 }, 1)
    }

    func testClockRollbackDoesNotPermanentlyThrottle() async throws {
        let now = Locked(1000.0)
        let config = NonaConfig(options: try options(interval: 60), store: InMemorySnapshotStore(),
                                http: StubHTTP { _, _ in NonaHTTPResponse(statusCode: 200, body: body()) },
                                clock: { now.withLock { $0 } })
        try await config.fetch()
        now.withLock { $0 = 500 }
        let status = try await config.fetch()
        XCTAssertEqual(status, .success)
    }

    func testUpdatesReachIndependentSubscribers() async throws {
        let config = try client()
        var first = config.updates().makeAsyncIterator()
        var second = config.updates().makeAsyncIterator()
        try await config.fetchAndActivate()
        let a = await first.next(), b = await second.next()
        XCTAssertEqual(a, ["flag"])
        XCTAssertEqual(b, ["flag"])
    }

    func testSlowSubscriberKeepsAllChangedKeysIncludingDeletions() async throws {
        let values = Locked(["a": NonaEntry(value: "0"), "b": NonaEntry(value: "0")])
        let config = NonaConfig(options: try options(), store: InMemorySnapshotStore(), http: StubHTTP { _, _ in
            NonaHTTPResponse(statusCode: 200, body: try JSONEncoder().encode(values.withLock { $0 }))
        })
        try await config.fetchAndActivate()
        var slow = config.updates().makeAsyncIterator()
        var fast = config.updates().makeAsyncIterator()
        values.withLock { $0["a"] = NonaEntry(value: "1") }
        try await config.fetchAndActivate()
        let first = await fast.next()
        XCTAssertEqual(first, ["a"])
        values.withLock { _ = $0.removeValue(forKey: "b") }
        try await config.fetchAndActivate()
        let second = await fast.next()
        let combined = await slow.next()
        XCTAssertEqual(second, ["b"])
        XCTAssertEqual(combined, ["a", "b"])
    }

    func testResetAfterPendingDiskWriteCannotResurrectCache() async throws {
        let store = BlockingStore()
        store.operation.withLock { $0 = "write" }
        let config = try client(store: store)
        let fetch = Task { try await config.fetch() }
        await fulfillment(of: [store.entered], timeout: 5)
        let started = expectation(description: "Reset requested")
        let reset = Task { started.fulfill(); try await config.reset() }
        await fulfillment(of: [started], timeout: 5)
        store.resume.signal()
        _ = try await fetch.value
        try await reset.value
        XCTAssertNil(store.read())
        XCTAssertFalse(config.activate())
        XCTAssertEqual(config.getSource("flag"), .static)
    }

    func testConcurrentNotificationProducersKeepEveryKey() async {
        let holder = Locked<UpdateSubscriber?>(nil)
        let stream = AsyncStream<Set<String>>(bufferingPolicy: .bufferingNewest(1)) { continuation in
            holder.withLock { $0 = UpdateSubscriber(continuation) }
        }
        let subscriber = holder.withLock { $0! }
        await withTaskGroup(of: Void.self) { group in
            for index in 0..<100 {
                group.addTask { subscriber.send([String(index)]) }
            }
        }
        var iterator = stream.makeAsyncIterator()
        let keys = await iterator.next()
        XCTAssertEqual(keys, Set((0..<100).map(String.init)))
        subscriber.finish()
        let finished = await iterator.next()
        XCTAssertNil(finished)
    }

    func testStreamDoesNotRetainClient() async throws {
        var config: NonaConfig? = try client()
        weak var reference = config
        var iterator = config!.updates().makeAsyncIterator()
        config = nil
        XCTAssertNil(reference)
        reference = nil
        let finished = await iterator.next()
        XCTAssertNil(finished)
    }

    func testURLSelectorsAndCacheIdentity() throws {
        let opts = try NonaOptions(baseURL: URL(string: "https://NONA.test:443/proxy%2Ftenant/")!,
                                  environmentID: "pre production", apiKey: "public", useReleases: true,
                                  releaseVersion: "1.4.x", prefix: "A&B")
        XCTAssertEqual(opts.baseURL.absoluteString, "https://nona.test/proxy%2Ftenant")
        let components = URLComponents(url: opts.snapshotURL, resolvingAgainstBaseURL: false)!
        XCTAssertEqual(components.percentEncodedPath, "/proxy%2Ftenant/api/pre%20production/releases/parameters")
        XCTAssertEqual(components.queryItems?.first?.value, "1.4.x")
        XCTAssertEqual(components.queryItems?.last?.value, "A&B")
        XCTAssertFalse(opts.cacheIdentity.contains("public"))
        XCTAssertEqual(try options().cacheIdentity, try options().cacheIdentity)
        XCTAssertNotEqual(try options().cacheIdentity, try options(key: "other").cacheIdentity)

        let working = try options()
        XCTAssertEqual(working.snapshotURL.path, "/api/Production/parameters")
        let activeRelease = try NonaOptions(baseURL: working.baseURL, environmentID: working.environmentID,
                                            apiKey: working.apiKey, useReleases: true)
        XCTAssertEqual(activeRelease.snapshotURL.path, "/api/Production/releases/parameters")
        XCTAssertNil(URLComponents(url: activeRelease.snapshotURL, resolvingAgainstBaseURL: false)?.query)
        XCTAssertNotEqual(working.cacheIdentity, activeRelease.cacheIdentity)
        XCTAssertNotEqual(activeRelease.cacheIdentity, opts.cacheIdentity)
    }

    func testOptionsAndFetchOverridesValidate() async throws {
        for url in ["file:///tmp/config", "https://user:password@nona.test", "https://nona.test/?query=1", "https://nona.test/#fragment"] {
            XCTAssertThrowsError(try NonaOptions(baseURL: URL(string: url)!, environmentID: "Production"))
        }
        for environment in ["", " ", ".", ".."] {
            XCTAssertThrowsError(try NonaOptions(baseURL: URL(string: "https://nona.test")!, environmentID: environment))
        }
        for key in ["key\r\nInjected: value", "key\0", "key\t", "key\u{7F}"] {
            XCTAssertThrowsError(try NonaOptions(baseURL: URL(string: "https://nona.test")!,
                                                environmentID: "Production", apiKey: key))
        }
        XCTAssertThrowsError(try NonaOptions(baseURL: URL(string: "https://nona.test")!,
                                            environmentID: "Production", useReleases: false,
                                            releaseVersion: "1.0.0"))
        let config = try client()
        for interval in [-1.0, .infinity, .nan] {
            do { try await config.fetch(minimumFetchInterval: interval); XCTFail() } catch is NonaError {} catch { XCTFail() }
        }
    }
}

extension NonaConfigTests {
    func testSharedSnapshotContract() async throws {
        struct Entry: Decodable { let value: String; let contentType: String }
        struct Case: Decodable { let name: String; let body: String; let valid: Bool; let expected: [String: Entry] }
        struct Corpus: Decodable { let snapshots: [Case] }
        let corpus = try JSONDecoder().decode(Corpus.self, from: Data(SharedContracts.json.utf8))
        for item in corpus.snapshots {
            let config = NonaConfig(options: try options(), store: InMemorySnapshotStore(), http: StubHTTP { _, _ in
                NonaHTTPResponse(statusCode: 200, body: Data(item.body.utf8), etag: "etag")
            })
            if !item.valid {
                do { try await config.fetch(); XCTFail("Expected rejection: \(item.name)") }
                catch let error as NonaError { XCTAssertEqual(error, .invalidSnapshot, item.name) }
                XCTAssertFalse(config.activate(), item.name)
                continue
            }
            try await config.fetchAndActivate()
            XCTAssertEqual(config.keys, Set(item.expected.keys), item.name)
            for (key, expected) in item.expected {
                guard case .success(let value, _, let type) = config.resolveString(key) else { XCTFail(item.name); continue }
                XCTAssertEqual(value, expected.value, item.name)
                XCTAssertEqual(type, expected.contentType, item.name)
            }
        }
    }

    func testSharedValueContract() async throws {
        struct Case: Decodable { let value: String; let long: String?; let double: Double?; let boolean: Bool }
        struct Corpus: Decodable { let values: [Case] }
        let corpus = try JSONDecoder().decode(Corpus.self, from: Data(SharedContracts.json.utf8))
        for item in corpus.values {
            let config = try client(item.value)
            try await config.fetchAndActivate()
            XCTAssertEqual(config.getLong("flag"), item.long.flatMap(Int64.init) ?? 0, item.value)
            XCTAssertEqual(config.getDouble("flag"), item.double ?? 0, item.value)
            XCTAssertEqual(config.getBoolean("flag"), item.boolean, item.value)
        }
    }
}

extension NonaConfigTests {
    func testFailedDiskWritesPreserveMemoryAndRecover() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let parent = root.appendingPathComponent("blocked")
        try Data("not a directory".utf8).write(to: parent)
        let store = FileSnapshotStore(fileURL: parent.appendingPathComponent("snapshot.json"))
        let config = try client(store: store)
        try await config.fetchAndActivate()
        XCTAssertEqual(config.getString("flag"), "A")
        XCTAssertNil(store.read())
        try FileManager.default.removeItem(at: parent)
        try await config.fetch()
        let restarted = try client(store: store)
        let restored = try await restarted.initialize()
        XCTAssertTrue(restored)
        XCTAssertEqual(restarted.getString("flag"), "A")
    }

    func testCancelledResetWaitingForWriteDoesNotClearCommittedValues() async throws {
        let store = BlockingStore()
        let config = try client(store: store)
        store.operation.withLock { $0 = "write" }
        let fetch = Task { try await config.fetch() }
        await fulfillment(of: [store.entered], timeout: 5)
        XCTAssertTrue(config.activate())
        let started = expectation(description: "Reset started")
        let reset = Task { started.fulfill(); try await config.reset() }
        await fulfillment(of: [started], timeout: 5)
        reset.cancel()
        store.resume.signal()
        _ = try await fetch.value
        do { try await reset.value; XCTFail("Expected cancellation") } catch is CancellationError {}
        XCTAssertEqual(config.getString("flag"), "A")
        XCTAssertNotNil(store.read())
    }

    func testConcurrentFileStoreInstancesNeverExposePartialJSON() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let path = root.appendingPathComponent("snapshot.json")
        let a = FileSnapshotStore(fileURL: path), b = FileSnapshotStore(fileURL: path)
        let payloads = [body(String(repeating: "A", count: 8192)), body(String(repeating: "B", count: 8192))]
        a.write(payloads[0])
        await withTaskGroup(of: Void.self) { group in
            for index in 0..<50 {
                group.addTask {
                    (index.isMultiple(of: 2) ? a : b).write(payloads[index % 2])
                    XCTAssertTrue(a.read().map(payloads.contains) ?? false)
                }
            }
        }
    }

    func testCacheRejectsWrongValueTypeWithoutLosingDefaults() async throws {
        let store = InMemorySnapshotStore()
        let config = try client(store: store)
        try await config.fetch()
        var json = try XCTUnwrap(JSONSerialization.jsonObject(with: XCTUnwrap(store.read())) as? [String: Any])
        json["values"] = ["flag": ["value": 123, "contentType": "text"]]
        store.write(try JSONSerialization.data(withJSONObject: json))
        let restarted = try client(store: store)
        restarted.setDefaults(["flag": "default"])
        let restored = try await restarted.initialize()
        XCTAssertFalse(restored)
        XCTAssertEqual(restarted.getString("flag"), "default")
    }
}

extension NonaConfigTests {
    func testRepeatedSubscriberCancellationDoesNotBreakFutureDelivery() async throws {
        let config = try client()
        for _ in 0..<200 {
            let stream = config.updates()
            let consumer = Task { var iterator = stream.makeAsyncIterator(); return await iterator.next() }
            consumer.cancel()
            let result = await consumer.value
            XCTAssertNil(result)
        }
        var iterator = config.updates().makeAsyncIterator()
        try await config.fetchAndActivate()
        let delivered = await iterator.next()
        XCTAssertEqual(delivered, ["flag"])
    }
}

extension NonaConfigTests {
    func testCacheWrittenByV1SDKIsIgnoredAfterIdentityMigration() async throws {
        struct Fixture: Decodable { let baselineRef: String; let json: String }
        struct Corpus: Decodable { let cacheFixtures: [String: Fixture] }
        let corpus = try JSONDecoder().decode(Corpus.self, from: Data(SharedContracts.json.utf8))
        let fixture = try XCTUnwrap(corpus.cacheFixtures["swift"])
        let store = InMemorySnapshotStore()
        store.write(Data(fixture.json.utf8))
        let options = try NonaOptions(baseURL: URL(string: "https://nona.test")!, environmentID: "Production")
        let config = NonaConfig(options: options, store: store, http: StubHTTP { _, _ in
            XCTFail("Restore must not use HTTP"); throw NonaError.transport
        })
        let restored = try await config.initialize()
        XCTAssertFalse(restored, fixture.baselineRef)
        XCTAssertEqual(config.getString("flag"), "")
    }
}
