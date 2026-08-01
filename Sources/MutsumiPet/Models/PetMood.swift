import Foundation

enum PetMood: Int, CaseIterable, Equatable {
    case sleepy
    case curious
    case pleased

    var symbol: String {
        switch self {
        case .sleepy: "zzz"
        case .curious: "?"
        case .pleased: "♪"
        }
    }
}
