enum PetPose: String, CaseIterable, Hashable {
    case idle
    case curious
    case happy
    case sleeping
    case grabbed

    var assetName: String {
        switch self {
        case .idle: "mutsumi_pet"
        case .curious: "mutsumi_curious"
        case .happy: "mutsumi_happy"
        case .sleeping: "mutsumi_sleeping"
        case .grabbed: "mutsumi_grabbed"
        }
    }
}
