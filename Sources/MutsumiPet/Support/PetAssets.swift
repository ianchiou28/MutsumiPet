import AppKit
import Foundation

@MainActor
enum PetAssets {
    private static let images: [PetPose: NSImage] = Dictionary(
        uniqueKeysWithValues: PetPose.allCases.map { pose in
            (pose, load(named: pose.assetName))
        }
    )
    private static let activityFrames: [PetActivity: [NSImage]] = Dictionary(
        uniqueKeysWithValues: [PetActivity.walking, .drinkingTea, .eatingSnack].map { activity in
            (activity, loadStripFrames(named: activity.stripAssetName ?? ""))
        }
    )

    static func character(for pose: PetPose) -> NSImage {
        images[pose] ?? images[.idle] ?? NSImage(size: NSSize(width: 1, height: 1))
    }

    static func character(for activity: PetActivity, frame: Int, fallback pose: PetPose) -> NSImage {
        guard let frames = activityFrames[activity], frames.isEmpty == false else {
            return character(for: pose)
        }
        return frames[min(max(frame, 0), frames.count - 1)]
    }

    private static func load(named name: String) -> NSImage {
        let bundleName = "MutsumiPet_MutsumiPet.bundle"
        let standardURL = Bundle.main.resourceURL?
            .appendingPathComponent(bundleName, isDirectory: true)
            .appendingPathComponent("\(name).png")

        if let standardURL, let image = NSImage(contentsOf: standardURL) {
            return image
        }

        if let moduleURL = Bundle.module.url(forResource: name, withExtension: "png"),
           let image = NSImage(contentsOf: moduleURL) {
            return image
        }

        return NSImage(size: NSSize(width: 1, height: 1))
    }

    private static func loadStripFrames(named name: String) -> [NSImage] {
        let strip = load(named: name)
        guard let data = strip.tiffRepresentation,
              let representation = NSBitmapImageRep(data: data),
              let cgImage = representation.cgImage else { return [] }

        let frameCount = 4
        let frameWidth = cgImage.width / frameCount
        guard frameWidth > 0 else { return [] }

        return (0..<frameCount).compactMap { index in
            let rect = CGRect(
                x: CGFloat(index * frameWidth),
                y: 0,
                width: CGFloat(frameWidth),
                height: CGFloat(cgImage.height)
            )
            guard let cropped = cgImage.cropping(to: rect) else { return nil }
            return NSImage(
                cgImage: cropped,
                size: NSSize(width: CGFloat(frameWidth), height: CGFloat(cgImage.height))
            )
        }
    }
}
