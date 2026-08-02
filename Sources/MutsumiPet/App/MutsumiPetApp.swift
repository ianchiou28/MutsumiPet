import AppKit
import SwiftUI

@main
struct MutsumiPetApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var store = PetStore()
    @StateObject private var windowController = PetWindowController()

    var body: some Scene {
        WindowGroup("若叶睦桌宠", id: "pet") {
            PetView(store: store, windowController: windowController)
        }
        .windowStyle(.hiddenTitleBar)
        .windowResizability(.contentSize)
        .commands {
            CommandGroup(replacing: .newItem) { }
            CommandMenu("若叶睦") {
                Button("和睦互动") { store.reactToTap() }
                    .keyboardShortcut("m", modifiers: [.command])
                Button(store.showsBubble ? "隐藏气泡" : "显示气泡") { store.toggleBubble() }
                    .keyboardShortcut("b", modifiers: [.command])
            }
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        NSApp.activate(ignoringOtherApps: true)
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }
}
