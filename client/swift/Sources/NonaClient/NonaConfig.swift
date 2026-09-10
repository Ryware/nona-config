import Foundation

/// Thread-safe synchronous reads with explicit asynchronous fetch and activation.
public final class NonaConfig: Sendable {
    private struct State {
        var active: Snapshot?
        var pending: Snapshot?
        var defaults: [String: NonaEntry] = [:]
        var generation: UInt64 = 0
        var lastFetch: TimeInterval?
        var subscribers: [UUID: UpdateSubscriber] = [:]
    }

    public let options: NonaOptions
    private let http: any NonaHTTPClient
    private let store: any NonaSnapshotStore
    private let clock: @Sendable () -> TimeInterval
    private let state = Locked(State())
    // Lock ordering is cacheAccess -> state. Synchronous readers never acquire cacheAccess.
    private let cacheAccess = SerialAccess()
    private let fetchGate = FetchGate()
    private let identity: String

    public init(options: NonaOptions, store: (any NonaSnapshotStore)? = nil,
                http: (any NonaHTTPClient)? = nil,
                clock: @escaping @Sendable () -> TimeInterval = { Date().timeIntervalSince1970 }) {
        self.options = options
        self.identity = options.cacheIdentity
        self.http = http ?? URLSessionHTTPClient(options: options)
        self.clock = clock
        if let store {
            self.store = store
        } else if let directory = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first {
            self.store = FileSnapshotStore(fileURL: directory.appendingPathComponent("NonaClient", isDirectory: true)
                .appendingPathComponent("snapshot-\(identity).json"))
        } else {
            self.store = InMemorySnapshotStore()
        }
    }

    deinit {
        let subscribers = state.withLock { Array($0.subscribers.values) }
        subscribers.forEach { $0.finish() }
    }

    public var keys: Set<String> {
        state.withLock { Set(($0.active?.values ?? [:]).keys).union($0.defaults.keys) }
    }

    public func setDefaults(_ values: [String: NonaDefaultValue]) {
        state.withLock { $0.defaults = values.mapValues(\.entry) }
    }

    /// A separate stream per subscriber. Slow consumers receive the union of buffered changed keys.
    public func updates() -> AsyncStream<Set<String>> {
        let id = UUID()
        return AsyncStream(bufferingPolicy: .bufferingNewest(1)) { continuation in
            state.withLock { $0.subscribers[id] = UpdateSubscriber(continuation) }
            continuation.onTermination = { [weak self] _ in
                self?.state.withLock { _ = $0.subscribers.removeValue(forKey: id) }
            }
        }
    }

    /// Restores the last successful fetch, without a network request. Call before the first fetch.
    @discardableResult
    public func initialize() async throws -> Bool {
        try await background { [self] in
            try cacheAccess.withLock {
                try Task.checkCancellation()
                guard state.withLock({ $0.active == nil && $0.pending == nil }),
                      let data = store.read(),
                      let snapshot = try? JSONDecoder().decode(Snapshot.self, from: data),
                      snapshot.identity == identity, snapshot.fetchedAt.isFinite else { return false }
                return try state.withLock { state in
                    try Task.checkCancellation()
                    state.active = snapshot
                    state.lastFetch = snapshot.fetchedAt
                    return true
                }
            }
        }
    }

    @discardableResult
    public func fetch(minimumFetchInterval: TimeInterval? = nil) async throws -> NonaFetchStatus {
        let interval = minimumFetchInterval ?? options.minimumFetchInterval
        guard interval.isFinite, interval >= 0 else {
            throw NonaError.invalidOptions("minimumFetchInterval must be finite and nonnegative.")
        }
        try await fetchGate.acquire()
        do {
            try Task.checkCancellation()
            let result = try await load(minimumFetchInterval: interval)
            await fetchGate.release()
            return result
        } catch {
            await fetchGate.release()
            throw error
        }
    }

    /// Returns true when an activated value or its content type changed.
    @discardableResult
    public func activate() -> Bool {
        let (changed, subscribers) = state.withLock { state -> (Set<String>, [UpdateSubscriber]) in
            guard let next = state.pending else { return ([], []) }
            let previous = state.active?.values ?? [:]
            let changed = Set(previous.keys).union(next.values.keys).filter { previous[$0] != next.values[$0] }
            state.pending = nil
            state.active = next
            return (changed, Array(state.subscribers.values))
        }
        // Never invoke stream termination handlers while holding the state lock.
        if !changed.isEmpty { subscribers.forEach { $0.send(changed) } }
        return !changed.isEmpty
    }

    @discardableResult
    public func fetchAndActivate(minimumFetchInterval: TimeInterval? = nil) async throws -> Bool {
        guard try await fetch(minimumFetchInterval: minimumFetchInterval) != .discarded else { return false }
        try Task.checkCancellation()
        return activate()
    }

    /// Clears memory and disk; results of requests already in flight are discarded.
    public func reset() async throws {
        try await background { [self] in
            try cacheAccess.withLock {
                try state.withLock { state in
                    try Task.checkCancellation()
                    state.generation &+= 1
                    state.active = nil
                    state.pending = nil
                    state.lastFetch = nil
                }
                store.clear()
            }
        }
    }

    public func getString(_ key: String) -> String { value(key, parse: { $0 }, zero: "") }
    public func getBoolean(_ key: String) -> Bool { value(key, parse: Self.boolean, zero: false) }
    public func getLong(_ key: String) -> Int64 { value(key, parse: { Int64(Self.trim($0)) }, zero: 0) }
    public func getDouble(_ key: String) -> Double { value(key, parse: Self.double, zero: 0) }

    /// Raw entry origin; typed getters can fall back when the remote value is malformed.
    public func getSource(_ key: String) -> NonaValueSource {
        state.withLock { entry(key, in: $0)?.1 ?? .static }
    }

    public func resolveString(_ key: String) -> NonaResolution<String> { resolve(key, parse: { $0 }) }
    public func resolveBoolean(_ key: String) -> NonaResolution<Bool> { resolve(key, parse: Self.boolean) }
    public func resolveLong(_ key: String) -> NonaResolution<Int64> { resolve(key, parse: { Int64(Self.trim($0)) }) }
    public func resolveDouble(_ key: String) -> NonaResolution<Double> { resolve(key, parse: Self.double) }

    private func value<T>(_ key: String, parse: (String) -> T?, zero: T) -> T {
        state.withLock { state in
            if let entry = state.active?.values[key], let value = parse(entry.value) { return value }
            if let entry = state.defaults[key], let value = parse(entry.value) { return value }
            return zero
        }
    }

    private func resolve<T: Sendable>(_ key: String, parse: (String) -> T?) -> NonaResolution<T> {
        state.withLock { state in
            guard let (entry, source) = entry(key, in: state) else {
                return .failure(reason: state.active == nil ? .notReady : .notFound,
                                message: "No readable value for '\(key)'. Client snapshots contain only frontend-scoped entries.")
            }
            guard let value = parse(entry.value) else {
                return .failure(reason: .typeMismatch, message: "Nona value '\(key)' has the wrong type.")
            }
            return .success(value: value, source: source, contentType: entry.contentType)
        }
    }

    private func entry(_ key: String, in state: State) -> (NonaEntry, NonaValueSource)? {
        if let entry = state.active?.values[key] { return (entry, .remote) }
        if let entry = state.defaults[key] { return (entry, .default) }
        return nil
    }

    private static func trim(_ value: String) -> String { value.trimmingCharacters(in: .whitespacesAndNewlines) }
    private static func boolean(_ value: String) -> Bool? {
        switch trim(value).lowercased() { case "true": return true; case "false": return false; default: return nil }
    }
    private static func double(_ value: String) -> Double? {
        let text = trim(value)
        // Match the decimal grammar used by the Kotlin SDK, excluding hex and suffixes.
        guard text.range(of: #"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$"#,
                         options: .regularExpression) != nil,
              let result = Double(text), result.isFinite else { return nil }
        return result
    }

    private func load(minimumFetchInterval: TimeInterval) async throws -> NonaFetchStatus {
        let now = clock()
        let (generation, previous, lastFetch) = state.withLock { ($0.generation, $0.pending ?? $0.active, $0.lastFetch) }
        if let lastFetch, now >= lastFetch, now - lastFetch < minimumFetchInterval { return .throttled }
        var headers: [String: String] = [:]
        if let apiKey = options.apiKey { headers["X-Api-Key"] = apiKey }
        if let etag = previous?.etag { headers["If-None-Match"] = etag }
        let response = try await http.get(url: options.snapshotURL, headers: headers)
        try Task.checkCancellation()
        let values: [String: NonaEntry]
        let etag: String?
        if response.statusCode == 304 {
            guard let previous else { throw NonaError.unexpectedNotModified }
            values = previous.values
            etag = previous.etag
        } else {
            guard (200...299).contains(response.statusCode) else {
                let problem = try? JSONDecoder().decode(ProblemDetails.self, from: response.body)
                throw NonaError.http(statusCode: response.statusCode,
                                     errorCode: problem?.errorCode,
                                     detail: problem?.detail)
            }
            guard response.body.count <= options.maxResponseBytes else {
                throw NonaError.responseTooLarge(maxBytes: options.maxResponseBytes)
            }
            do { values = try JSONDecoder().decode([String: NonaEntry].self, from: response.body) }
            catch { throw NonaError.invalidSnapshot }
            etag = response.etag
        }
        let snapshot = Snapshot(identity: identity, values: values, etag: etag, fetchedAt: now)
        return try await background { [self] in
            try cacheAccess.withLock {
                let data = try? JSONEncoder().encode(snapshot)
                let committed = try state.withLock { state -> Bool in
                    try Task.checkCancellation()
                    guard state.generation == generation else { return false }
                    if response.statusCode == 304, state.pending == nil { state.active = snapshot }
                    else { state.pending = snapshot }
                    state.lastFetch = now
                    return true
                }
                guard committed else { return .discarded }
                // Keep writes ordered with reset/initialize without blocking value reads.
                if let data { store.write(data) }
                return response.statusCode == 304 ? .notModified : .success
            }
        }
    }
}

private struct ProblemDetails: Decodable {
    let errorCode: String?
    let detail: String?
}

/// Disk operations must not inherit the UI actor. Cancellation propagates to the worker.
private func background<T: Sendable>(_ operation: @escaping @Sendable () throws -> T) async throws -> T {
    let worker = Task.detached(priority: Task.currentPriority, operation: operation)
    return try await withTaskCancellationHandler(operation: { try await worker.value }, onCancel: { worker.cancel() })
}

/// FIFO fetch serialization; cancelled waiters do not start network requests.
private actor FetchGate {
    private var busy = false
    private var waiters: [(UUID, CheckedContinuation<Void, Error>)] = []

    func acquire() async throws {
        try Task.checkCancellation()
        if !busy { busy = true; return }
        let id = UUID()
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
                if Task.isCancelled { continuation.resume(throwing: CancellationError()) }
                else { waiters.append((id, continuation)) }
            }
        } onCancel: {
            Task { await self.cancel(id) }
        }
    }

    func release() {
        if waiters.isEmpty { busy = false }
        else { waiters.removeFirst().1.resume() }
    }

    private func cancel(_ id: UUID) {
        guard let index = waiters.firstIndex(where: { $0.0 == id }) else { return }
        waiters.remove(at: index).1.resume(throwing: CancellationError())
    }
}
