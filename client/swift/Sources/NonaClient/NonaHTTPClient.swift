import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

public struct NonaHTTPResponse: Sendable {
    public let statusCode: Int
    public let body: Data
    public let etag: String?
    public init(statusCode: Int, body: Data = Data(), etag: String? = nil) {
        self.statusCode = statusCode
        self.body = body
        self.etag = etag
    }
}

/// Custom transports must enforce their own redirect, timeout and response-size policies.
public protocol NonaHTTPClient: Sendable {
    func get(url: URL, headers: [String: String]) async throws -> NonaHTTPResponse
}

private final class RejectRedirects: NSObject, URLSessionTaskDelegate, Sendable {
    func urlSession(_ session: URLSession, task: URLSessionTask,
                    willPerformHTTPRedirection response: HTTPURLResponse, newRequest request: URLRequest,
                    completionHandler: @escaping (URLRequest?) -> Void) {
        completionHandler(nil)
    }
}

/// Dedicated ephemeral session: no shared cookies, credentials or HTTP cache.
public final class URLSessionHTTPClient: NonaHTTPClient {
    private let session: URLSession
    private let timeout: TimeInterval
    private let maxBytes: Int

    public init(options: NonaOptions) {
        timeout = options.requestTimeout
        maxBytes = options.maxResponseBytes
        let configuration = URLSessionConfiguration.ephemeral
        configuration.urlCache = nil
        configuration.httpCookieStorage = nil
        configuration.urlCredentialStorage = nil
        configuration.requestCachePolicy = .reloadIgnoringLocalCacheData
        configuration.timeoutIntervalForRequest = timeout
        configuration.timeoutIntervalForResource = timeout
        session = URLSession(configuration: configuration, delegate: RejectRedirects(), delegateQueue: nil)
    }

    deinit { session.invalidateAndCancel() }

    public func get(url: URL, headers: [String: String]) async throws -> NonaHTTPResponse {
        var request = URLRequest(url: url, cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: timeout)
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        for (name, value) in headers { request.setValue(value, forHTTPHeaderField: name) }
        do {
            #if canImport(FoundationNetworking)
            let (body, response) = try await session.data(for: request)
            guard let response = response as? HTTPURLResponse else { throw NonaError.transport }
            guard body.count <= maxBytes else { throw NonaError.responseTooLarge(maxBytes: maxBytes) }
            return NonaHTTPResponse(statusCode: response.statusCode,
                                    body: response.statusCode == 304 ? Data() : body,
                                    etag: response.value(forHTTPHeaderField: "ETag"))
            #else
            let (bytes, response) = try await session.bytes(for: request)
            guard let response = response as? HTTPURLResponse else { throw NonaError.transport }
            // Cancel unused bodies, including redirects and 304 responses.
            defer { bytes.task.cancel() }
            if response.statusCode == 304 {
                return NonaHTTPResponse(statusCode: response.statusCode)
            }
            var body = Data()
            for try await byte in bytes {
                guard body.count < maxBytes else { throw NonaError.responseTooLarge(maxBytes: maxBytes) }
                body.append(byte)
            }
            return NonaHTTPResponse(statusCode: response.statusCode, body: body,
                                    etag: response.value(forHTTPHeaderField: "ETag"))
            #endif
        } catch is CancellationError {
            throw CancellationError()
        } catch let error as URLError where error.code == .cancelled {
            throw CancellationError()
        } catch let error as NonaError {
            throw error
        } catch {
            throw NonaError.transport
        }
    }
}
