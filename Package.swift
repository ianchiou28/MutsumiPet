// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "MutsumiPet",
    platforms: [.macOS(.v13)],
    products: [
        .executable(name: "MutsumiPet", targets: ["MutsumiPet"])
    ],
    targets: [
        .executableTarget(
            name: "MutsumiPet",
            resources: [.process("Resources")]
        ),
        .testTarget(
            name: "MutsumiPetTests",
            dependencies: ["MutsumiPet"]
        )
    ]
)
