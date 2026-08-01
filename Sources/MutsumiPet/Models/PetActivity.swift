import CoreGraphics

enum PetActivity: String, Hashable {
    case idle
    case walking
    case drinkingTea
    case eatingSnack

    var frameSequence: [Int] {
        switch self {
        case .idle: [0]
        case .walking: [0, 1, 2, 3]
        case .drinkingTea, .eatingSnack:
            [0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 2,
             3, 3, 3, 3, 3, 3, 3, 2, 2, 1, 1, 0, 0, 0, 0]
        }
    }

    /// Offsets are normalized to the generated sheet height so the character's
    /// visual center and feet stay fixed when adjacent frames have different bounds.
    func alignmentOffset(for frame: Int) -> CGSize {
        let offsets: [CGSize]
        switch self {
        case .idle:
            offsets = [.zero]
        case .walking:
            offsets = [
                CGSize(width: -46.5, height: 3),
                CGSize(width: -13, height: -6),
                CGSize(width: 20, height: 11),
                CGSize(width: 40.5, height: -7)
            ]
        case .drinkingTea:
            offsets = [
                CGSize(width: -18, height: 0),
                CGSize(width: -5, height: 0),
                CGSize(width: 10, height: 0),
                CGSize(width: 13, height: 0)
            ]
        case .eatingSnack:
            offsets = [
                CGSize(width: -9, height: 0),
                CGSize(width: -9, height: 1),
                CGSize(width: 2.5, height: 0),
                CGSize(width: 15.5, height: 0)
            ]
        }
        let offset = offsets[min(max(frame, 0), offsets.count - 1)]
        return CGSize(width: offset.width / 724, height: offset.height / 724)
    }

    /// The generated walk sheet faces screen-left in its unmirrored form.
    func horizontalScale(walkingDirection: Int) -> CGFloat {
        guard self == .walking else { return 1 }
        return walkingDirection < 0 ? 1 : -1
    }

    var stripAssetName: String? {
        switch self {
        case .idle: nil
        case .walking: "mutsumi_walk_strip"
        case .drinkingTea: "mutsumi_tea_strip"
        case .eatingSnack: "mutsumi_snack_strip"
        }
    }
}
