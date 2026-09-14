import Foundation
import NonaClient

private actor HTTP: NonaHTTPClient {
    var iteration = 0
    let stress: Bool
    init(stress: Bool) { self.stress = stress }
    func get(url: URL, headers: [String: String]) async throws -> NonaHTTPResponse {
        iteration += 1
        let value = stress ? String(repeating: iteration.isMultiple(of: 2) ? "A" : "B", count: 262144) : "compatible"
        return NonaHTTPResponse(statusCode: 200, body: try JSONEncoder().encode(["flag": NonaEntry(value: value)]))
    }
}

@main struct Probe {
    static func main() async throws {
        let mode = CommandLine.arguments[1]
        let path = URL(fileURLWithPath: CommandLine.arguments[2])
        let options = try NonaOptions(baseURL: URL(string: "https://nona.test")!, environmentID: "Production", minimumFetchInterval: 0)
        let config = NonaConfig(options: options, store: FileSnapshotStore(fileURL: path), http: HTTP(stress: mode == "stress"))
        if mode == "read" {
            guard try await config.initialize(), config.getString("flag") == "compatible" else { fatalError("Cache compatibility failed") }
        } else if mode == "stress" {
            try await config.fetch()
            try Data().write(to: path.appendingPathExtension("ready"))
            for _ in 0..<10000 { try await config.fetch() }
        } else if mode == "seed" {
            try await config.fetch()
        } else { fatalError("Unknown probe mode") }
        print("PASS: \(mode)")
    }
}
