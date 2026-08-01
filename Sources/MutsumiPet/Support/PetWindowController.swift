import AppKit
import SwiftUI

@MainActor
final class PetWindowController: ObservableObject {
    private weak var window: NSWindow?
    private var dragStartOrigin: NSPoint?
    private var dragStartMouseLocation: NSPoint?
    private var didPlaceWindow = false
    private var motion = WanderMotion()

    func configure(_ window: NSWindow, layerMode: WindowLayerMode) {
        let needsInitialConfiguration = self.window !== window
        self.window = window
        window.level = layerMode.windowLevel

        guard needsInitialConfiguration else { return }

        window.isOpaque = false
        window.backgroundColor = .clear
        window.hasShadow = false
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.styleMask = [.borderless, .nonactivatingPanel]
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        window.isMovableByWindowBackground = false
        window.isReleasedWhenClosed = false
        window.hidesOnDeactivate = false
        window.animationBehavior = .none
        window.contentView?.wantsLayer = true
        window.contentView?.layerContentsRedrawPolicy = .onSetNeedsDisplay

        guard didPlaceWindow == false, let screen = window.screen ?? NSScreen.main else { return }
        didPlaceWindow = true
        let visible = screen.visibleFrame
        let origin = NSPoint(
            x: visible.maxX - window.frame.width - 28,
            y: visible.minY + 24
        )
        window.setFrameOrigin(origin)
    }

    func updateLevel(_ layerMode: WindowLayerMode) {
        window?.level = layerMode.windowLevel
    }

    func beginDrag() {
        guard let window else { return }
        guard dragStartOrigin == nil else { return }
        stopWandering()
        dragStartOrigin = window.frame.origin
        dragStartMouseLocation = NSEvent.mouseLocation
    }

    func dragWindowToCurrentMouse() {
        guard let window,
              let startOrigin = dragStartOrigin,
              let startMouse = dragStartMouseLocation else { return }

        let mouse = NSEvent.mouseLocation
        let nextOrigin = NSPoint(
            x: startOrigin.x + mouse.x - startMouse.x,
            y: startOrigin.y + mouse.y - startMouse.y
        )
        let nextFrame = NSRect(origin: nextOrigin, size: window.frame.size)
        window.setFrame(nextFrame, display: false, animate: false)
    }

    func endDrag() {
        dragStartOrigin = nil
        dragStartMouseLocation = nil
        window?.displayIfNeeded()
    }

    func beginWandering() {
        motion.resetTarget()
    }

    func stopWandering() {
        motion.resetTarget()
    }

    func advanceWandering(speed: Double, timeStep: Double = 0.05) -> WanderStepResult {
        guard dragStartOrigin == nil, let window else { return .paused }
        let visible = bestScreen(for: window.frame)?.visibleFrame ?? NSScreen.main?.visibleFrame ?? .zero
        let minX = visible.minX
        let maxX = max(minX, visible.maxX - window.frame.width)
        let clampedY = min(max(window.frame.origin.y, visible.minY), max(visible.minY, visible.maxY - window.frame.height))

        motion.ensureTarget(currentX: window.frame.origin.x, minX: minX, maxX: maxX)
        let previousX = window.frame.origin.x
        switch motion.nextStep(
            currentX: previousX,
            maximumDistance: CGFloat(speed * timeStep)
        ) {
        case .paused:
            return .paused
        case .arrived(let targetX):
            window.setFrameOrigin(NSPoint(x: targetX, y: clampedY))
            return .arrived
        case .move(let nextX):
            window.setFrameOrigin(NSPoint(x: nextX, y: clampedY))
        }

        let appliedDelta = window.frame.origin.x - previousX
        guard abs(appliedDelta) > 0.01 else {
            motion.resetTarget()
            return .arrived
        }
        let appliedDirection = appliedDelta < 0 ? -1 : 1
        motion.recordApplied(deltaX: appliedDelta)
        return .moving(direction: appliedDirection, distance: abs(appliedDelta))
    }

    private func bestScreen(for frame: NSRect) -> NSScreen? {
        NSScreen.screens.max { lhs, rhs in
            lhs.frame.intersection(frame).width * lhs.frame.intersection(frame).height
                < rhs.frame.intersection(frame).width * rhs.frame.intersection(frame).height
        }
    }
}

enum WanderStepResult: Equatable {
    case paused
    case moving(direction: Int, distance: CGFloat)
    case arrived
}

struct WindowAccessor: NSViewRepresentable {
    let controller: PetWindowController
    let layerMode: WindowLayerMode

    func makeNSView(context: Context) -> NSView {
        let view = NSView(frame: .zero)
        DispatchQueue.main.async { [weak view] in
            if let window = view?.window {
                controller.configure(window, layerMode: layerMode)
            }
        }
        return view
    }

    func updateNSView(_ nsView: NSView, context: Context) {
        DispatchQueue.main.async { [weak nsView] in
            if let window = nsView?.window {
                controller.configure(window, layerMode: layerMode)
            }
        }
    }
}
