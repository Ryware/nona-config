// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "NonaClient",
    platforms: [.iOS(.v15), .macOS(.v12)],
    products: [.library(name: "NonaClient", targets: ["NonaClient"])],
    dependencies: [
        .package(url: "https://github.com/apple/swift-crypto.git", from: "3.0.0")
    ],
    targets: [
        .target(name: "NonaClient", dependencies: [
            .product(name: "Crypto", package: "swift-crypto", condition: .when(platforms: [.linux]))
        ], path: "client/swift/Sources/NonaClient"),
        .testTarget(name: "NonaClientTests", dependencies: ["NonaClient"],
                    path: "client/swift/Tests/NonaClientTests")
    ]
)
