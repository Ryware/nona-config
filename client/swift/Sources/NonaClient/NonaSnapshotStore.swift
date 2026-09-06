import Foundation

/// Implementations must be thread-safe. Cache operations are best effort and run off the main actor.
public protocol NonaSnapshotStore: Sendable {
    func read() -> Data?
    func write(_ data: Data)
    func clear()
}

// The sole unchecked boundary: every access to value is protected by the same lock.
final class Locked<Value>: @unchecked Sendable {
    private let lock = NSLock()
    private var value: Value
    init(_ value: Value) { self.value = value }
    func withLock<Result>(_ body: (inout Value) throws -> Result) rethrows -> Result {
        lock.lock()
        defer { lock.unlock() }
        return try body(&value)
    }
}

public final class InMemorySnapshotStore: NonaSnapshotStore {
    private let data = Locked<Data?>(nil)
    public init() {}
    public func read() -> Data? { data.withLock { $0 } }
    public func write(_ data: Data) { self.data.withLock { $0 = data } }
    public func clear() { data.withLock { $0 = nil } }
}

/// Use an application-private directory. Writes atomically replace the previous complete snapshot.
public final class FileSnapshotStore: NonaSnapshotStore {
    public let fileURL: URL
    private let access = Locked(())
    public init(fileURL: URL) { self.fileURL = fileURL }
    public func read() -> Data? { access.withLock { _ in try? Data(contentsOf: fileURL) } }
    public func write(_ data: Data) {
        access.withLock { _ in
            do {
                try FileManager.default.createDirectory(at: fileURL.deletingLastPathComponent(), withIntermediateDirectories: true)
                try data.write(to: fileURL, options: .atomic)
                var url = fileURL
                var values = URLResourceValues()
                values.isExcludedFromBackup = true
                try url.setResourceValues(values)
            } catch { /* A failed cache write must not discard valid in-memory configuration. */ }
        }
    }
    public func clear() { access.withLock { _ in _ = try? FileManager.default.removeItem(at: fileURL) } }
}

struct Snapshot: Codable, Sendable {
    let identity: String
    let values: [String: NonaEntry]
    let etag: String?
    let fetchedAt: TimeInterval
}
