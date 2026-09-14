import Foundation

/// Coalesces invalidations without an unbounded queue or lost changed-key sets.
final class UpdateSubscriber: Sendable {
    private let continuation: AsyncStream<Set<String>>.Continuation
    private let emission = SerialAccess()

    init(_ continuation: AsyncStream<Set<String>>.Continuation) {
        self.continuation = continuation
    }

    func send(_ keys: Set<String>) {
        emission.withLock {
            if case .dropped(let previous) = continuation.yield(keys) {
                // Only this emitter can replace the buffered value during the merge.
                // A consumer may receive keys first; the union then repeats keys safely.
                continuation.yield(previous.union(keys))
            }
        }
    }

    // Never finish under a lock: termination handlers can remove the subscription.
    func finish() { continuation.finish() }
}
