// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "NonaClient",
    platforms: [.iOS(.v15), .macOS(.v12)],
    products: [.library(name: "NonaClient", targets: ["NonaClient"])],
    targets: [
        .target(name: "NonaClient", path: "client/swift/Sources/NonaClient"),
        .testTarget(name: "NonaClientTests", dependencies: ["NonaClient"],
                    path: "client/swift/Tests/NonaClientTests")
    ]
)
