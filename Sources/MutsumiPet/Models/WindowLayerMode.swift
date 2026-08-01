import AppKit
import CoreGraphics

enum WindowLayerMode: String, CaseIterable, Identifiable {
    case front
    case normal
    case desktop

    var id: String { rawValue }

    var title: String {
        switch self {
        case .front: "始终置顶"
        case .normal: "普通窗口"
        case .desktop: "位于桌面后台"
        }
    }

    var windowLevel: NSWindow.Level {
        switch self {
        case .front:
            .floating
        case .normal:
            .normal
        case .desktop:
            NSWindow.Level(rawValue: Int(CGWindowLevelForKey(.desktopIconWindow)) + 1)
        }
    }
}
