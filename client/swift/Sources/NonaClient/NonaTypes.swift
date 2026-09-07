import Foundation

public enum NonaValueSource: String, Sendable { case remote, `default`, `static` }
public enum NonaFailureReason: String, Sendable { case notReady, notFound, typeMismatch }
public enum NonaFetchStatus: String, Sendable { case success, notModified, throttled, discarded }

public struct NonaEntry: Codable, Sendable, Equatable {
    public let value: String
    public let contentType: String

    public init(value: String, contentType: String = "text") {
        self.value = value
        self.contentType = contentType
    }

    private enum CodingKeys: String, CodingKey { case value, contentType }
    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        value = try values.decode(String.self, forKey: .value)
        contentType = try values.decodeIfPresent(String.self, forKey: .contentType) ?? "text"
    }
}

public enum NonaDefaultValue: Sendable, ExpressibleByStringLiteral, ExpressibleByBooleanLiteral,
                              ExpressibleByIntegerLiteral, ExpressibleByFloatLiteral {
    case string(String), boolean(Bool), integer(Int64), double(Double)
    public init(stringLiteral value: String) { self = .string(value) }
    public init(booleanLiteral value: Bool) { self = .boolean(value) }
    public init(integerLiteral value: Int64) { self = .integer(value) }
    public init(floatLiteral value: Double) { self = .double(value) }

    var entry: NonaEntry {
        switch self {
        case .string(let value): return NonaEntry(value: value)
        case .boolean(let value): return NonaEntry(value: String(value), contentType: "boolean")
        case .integer(let value): return NonaEntry(value: String(value), contentType: "number")
        case .double(let value): return NonaEntry(value: String(value), contentType: "number")
        }
    }
}

public enum NonaResolution<Value: Sendable>: Sendable {
    case success(value: Value, source: NonaValueSource, contentType: String)
    case failure(reason: NonaFailureReason, message: String)
}

public enum NonaError: Error, Sendable, Equatable, LocalizedError {
    case invalidOptions(String)
    case transport
    case http(statusCode: Int, errorCode: String? = nil, detail: String? = nil)
    case invalidSnapshot
    case responseTooLarge(maxBytes: Int)
    case unexpectedNotModified

    public var errorDescription: String? {
        switch self {
        case .invalidOptions(let message): return message
        case .transport: return "The Nona request failed. Cached values and defaults remain available."
        case .http(let code, _, let detail): return detail ?? "Nona returned HTTP \(code)."
        case .invalidSnapshot: return "Nona returned an invalid configuration snapshot."
        case .responseTooLarge(let limit): return "Nona response exceeds the \(limit)-byte limit."
        case .unexpectedNotModified: return "Nona returned 304 without a cached snapshot."
        }
    }

    public var statusCode: Int? {
        guard case .http(let statusCode, _, _) = self else { return nil }
        return statusCode
    }

    public var errorCode: String? {
        guard case .http(_, let errorCode, _) = self else { return nil }
        return errorCode
    }

    public var detail: String? {
        guard case .http(_, _, let detail) = self else { return nil }
        return detail
    }
}
