import NonaClient
import SwiftUI

@main
struct NonaSampleApp: App {
    var body: some Scene { WindowGroup { ContentView() } }
}

@MainActor
private final class SampleModel: ObservableObject {
    @Published var server = UserDefaults.standard.string(forKey: "nona.server") ?? "http://127.0.0.1:18686"
    @Published var key = UserDefaults.standard.string(forKey: "nona.key") ?? ""
    @Published var message = "Connect to your local Nona server to begin."
    @Published var flag = "default"
    @Published var source = "default"
    @Published var retries: Int64 = 3
    @Published var busy = false
    private var config: NonaConfig?
    private var work: Task<Void, Never>?

    func connect() {
        guard let url = URL(string: server.trimmingCharacters(in: .whitespacesAndNewlines)) else {
            message = "Enter a valid HTTP or HTTPS URL."; return
        }
        do {
            let options = try NonaOptions(baseURL: url, environmentID: "Production", apiKey: key,
                                          minimumFetchInterval: 0, requestTimeout: 3)
            let client = NonaConfig(options: options)
            client.setDefaults(["flag": "default", "Limits:Retries": 3])
            config = client
            UserDefaults.standard.set(options.baseURL.absoluteString, forKey: "nona.server")
            UserDefaults.standard.set(key, forKey: "nona.key")
            perform { "Cache restored: \(try await client.initialize())" }
        } catch { message = error.localizedDescription }
    }

    func fetch() { guard let config else { connect(); return }; perform { "Fetch: \(try await config.fetch().rawValue)" } }
    func activate() { guard let config else { return }; message = "Activated: \(config.activate())"; refresh() }
    func fetchAndActivate() { guard let config else { connect(); return }; perform { "Changed: \(try await config.fetchAndActivate())" } }
    func reset() { guard let config else { return }; perform { try await config.reset(); return "Cache cleared" } }
    func cancel() { work?.cancel() }

    private func perform(_ action: @escaping @Sendable () async throws -> String) {
        work?.cancel()
        busy = true
        work = Task {
            defer { busy = false }
            do { message = try await action() }
            catch is CancellationError { message = "Cancelled" }
            catch { message = error.localizedDescription }
            refresh()
        }
    }

    private func refresh() {
        flag = config?.getString("flag") ?? "default"
        source = config?.getSource("flag").rawValue ?? "default"
        retries = config?.getLong("Limits:Retries") ?? 3
    }
}

private struct ContentView: View {
    @StateObject private var model = SampleModel()
    var body: some View {
        NavigationView {
            Form {
                Section(header: Text("Connection"), footer: Text("Use only a frontend-scoped key. Local HTTP is enabled for this sample only.")) {
                    TextField("Server URL", text: $model.server).textInputAutocapitalization(.never).autocorrectionDisabled()
                        .keyboardType(.URL).accessibilityIdentifier("serverURL")
                    TextField("Frontend API key", text: $model.key).textInputAutocapitalization(.never).autocorrectionDisabled()
                        .accessibilityIdentifier("frontendKey")
                    Button("Connect & restore", action: model.connect).accessibilityIdentifier("connect")
                }.disabled(model.busy)
                Section("Active configuration") {
                    valueRow("Flag", model.flag)
                    valueRow("Source", model.source)
                    valueRow("Retries", String(model.retries))
                }
                Section("Actions") {
                    Button("Fetch", action: model.fetch)
                    Button("Activate", action: model.activate)
                    Button("Fetch & activate", action: model.fetchAndActivate)
                    Button("Reset cache", role: .destructive, action: model.reset)
                }.disabled(model.busy)
                Section("Status") {
                    if model.busy { ProgressView("Working…") }
                    Text(model.message).font(.callout).accessibilityIdentifier("status")
                }
            }
            .navigationTitle("Nona SDK")
            .onAppear { model.connect() }
            .onDisappear { model.cancel() }
        }.navigationViewStyle(.stack)
    }
    private func valueRow(_ name: String, _ value: String) -> some View {
        HStack { Text(name); Spacer(); Text(value).foregroundColor(.secondary) }
    }
}
