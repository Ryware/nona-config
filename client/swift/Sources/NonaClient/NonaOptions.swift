#if canImport(CryptoKit)
import CryptoKit
#elseif canImport(Crypto)
import Crypto
#endif
import Foundation

/// Use a frontend-scoped key: credentials shipped in an application are public.
public struct NonaOptions: Sendable {
    public let baseURL: URL
    public let environmentID: String
    public let apiKey: String?
    public let useReleases: Bool
    public let releaseVersion: String?
    public let prefix: String?
    public let minimumFetchInterval: TimeInterval
    public let requestTimeout: TimeInterval
    public let maxResponseBytes: Int

    public init(baseURL: URL, environmentID: String, apiKey: String? = nil,
                useReleases: Bool = false, releaseVersion: String? = nil, prefix: String? = nil,
                minimumFetchInterval: TimeInterval = 12 * 60 * 60,
                requestTimeout: TimeInterval = 10, maxResponseBytes: Int = 8 * 1024 * 1024) throws {
        guard var url = URLComponents(url: baseURL, resolvingAgainstBaseURL: false),
              let scheme = url.scheme?.lowercased(), ["http", "https"].contains(scheme),
              let host = url.host, !host.isEmpty, url.user == nil, url.password == nil,
              url.query == nil, url.fragment == nil else {
            throw NonaError.invalidOptions("baseURL must be an absolute HTTP(S) URL without credentials, query or fragment.")
        }
        guard !environmentID.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              environmentID != ".", environmentID != ".." else {
            throw NonaError.invalidOptions("environmentID must be nonempty and cannot be a dot path segment.")
        }
        guard minimumFetchInterval.isFinite, minimumFetchInterval >= 0,
              requestTimeout.isFinite, requestTimeout > 0, maxResponseBytes > 0 else {
            throw NonaError.invalidOptions("Intervals must be finite; fetch interval must be nonnegative, timeout and response limit positive.")
        }
        if let apiKey, apiKey.unicodeScalars.contains(where: { CharacterSet.controlCharacters.contains($0) }) {
            throw NonaError.invalidOptions("apiKey cannot contain control characters.")
        }
        let normalizedReleaseVersion = releaseVersion?.trimmingCharacters(in: .whitespacesAndNewlines)
        let selectedRelease = normalizedReleaseVersion.flatMap { $0.isEmpty ? nil : $0 }
        if useReleases, selectedRelease == "." || selectedRelease == ".." {
            throw NonaError.invalidOptions("releaseVersion cannot be a dot path segment.")
        }
        url.scheme = scheme
        url.host = host.lowercased()
        if (scheme == "https" && url.port == 443) || (scheme == "http" && url.port == 80) { url.port = nil }
        while url.percentEncodedPath.hasSuffix("/") { url.percentEncodedPath.removeLast() }
        guard let normalized = url.url else { throw NonaError.invalidOptions("Invalid baseURL.") }
        self.baseURL = normalized
        self.environmentID = environmentID
        self.apiKey = apiKey
        self.useReleases = useReleases
        self.releaseVersion = selectedRelease
        self.prefix = prefix
        self.minimumFetchInterval = minimumFetchInterval
        self.requestTimeout = requestTimeout
        self.maxResponseBytes = maxResponseBytes
    }

    var snapshotURL: URL {
        // All components were validated in init; preserve the server's escaped base path.
        var url = URLComponents(url: baseURL, resolvingAgainstBaseURL: false)!
        let unreserved = CharacterSet(charactersIn: "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~")
        url.percentEncodedPath += "/api/environments/" + environmentID.addingPercentEncoding(withAllowedCharacters: unreserved)!
        if useReleases {
            let release = releaseVersion ?? "active"
            url.percentEncodedPath += "/releases/" + release.addingPercentEncoding(withAllowedCharacters: unreserved)! + "/parameters"
        } else {
            url.percentEncodedPath += "/parameters"
        }
        var query: [URLQueryItem] = []
        if let prefix { query.append(URLQueryItem(name: "prefix", value: prefix)) }
        if !query.isEmpty { url.queryItems = query }
        return url.url!
    }

    var cacheIdentity: String {
        var data = Data()
        let effectiveReleaseVersion = useReleases ? releaseVersion : nil
        for part in ["nona-swift-cache-v2", baseURL.absoluteString, apiKey, environmentID,
                     String(useReleases), prefix, effectiveReleaseVersion] {
            let bytes = part.map { Data($0.utf8) }
            data.append(Data("\(bytes?.count ?? -1):".utf8))
            if let bytes { data.append(bytes) }
        }
        return SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
    }
}
