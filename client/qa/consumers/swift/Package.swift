// swift-tools-version: 5.9
import PackageDescription
let package = Package(name: "NonaConsumerProbe", platforms: [.macOS(.v12)],
    dependencies: [.package(name: "NonaClient", path: "../../../..")],
    targets: [.executableTarget(name: "Probe", dependencies: [.product(name: "NonaClient", package: "NonaClient")])])
