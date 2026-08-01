import AppKit
import Combine
import SwiftUI

struct PetView: View {
    @ObservedObject var store: PetStore
    @ObservedObject var windowController: PetWindowController
    @State private var didDrag = false
    @State private var dropBounce = 0.0

    private let lifestyleTimer = Timer.publish(every: 18, on: .main, in: .common).autoconnect()
    private let activityAnimationTimer = Timer.publish(every: 0.125, on: .main, in: .common).autoconnect()
    private let walkingTimer = Timer.publish(every: 1.0 / 60.0, on: .main, in: .common).autoconnect()
    private let llmTimer = Timer.publish(every: 60, on: .main, in: .common).autoconnect()

    var body: some View {
        ZStack(alignment: .bottom) {
            Color.clear

            if store.showsBubble {
                ThoughtBubble(text: store.message, mood: store.mood)
                    .frame(maxWidth: 270)
                    .padding(.bottom, 326)
                    .allowsHitTesting(false)
            }

            Image(nsImage: PetAssets.character(for: store.activity, frame: store.animationFrame, fallback: store.pose))
                .resizable()
                .interpolation(.high)
                .scaledToFit()
                .frame(width: 314, height: 314)
                .scaleEffect(x: characterHorizontalScale, y: 1)
                .offset(
                    x: activityFrameOffset.width * 314 * characterHorizontalScale,
                    y: (store.pose == .grabbed ? -15 : store.mood == .curious ? -3 : 0)
                        + activityFrameOffset.height * 314
                        + dropBounce
                )
                .shadow(color: .black.opacity(store.pose == .grabbed ? 0 : 0.16), radius: store.pose == .grabbed ? 0 : 10, y: 5)
                .contentShape(Rectangle())
                .onTapGesture {
                    guard didDrag == false else { didDrag = false; return }
                    store.reactToTap()
                }
                .simultaneousGesture(dragGesture)
                .contextMenu { contextMenu }
                .accessibilityLabel("Q版若叶睦桌宠")
                .accessibilityHint("点击与她互动，拖动可移动位置")

            WindowAccessor(controller: windowController, layerMode: store.layerMode)
                .frame(width: 0, height: 0)
        }
        .frame(width: 380, height: 450)
        .scaleEffect(store.scale, anchor: .bottomTrailing)
        .frame(width: 380 * store.scale, height: 450 * store.scale, alignment: .bottomTrailing)
        .background(Color.clear)
        .onChange(of: store.layerMode) { newValue in
            windowController.updateLevel(newValue)
        }
        .onChange(of: store.activity) { activity in
            if activity == .walking {
                windowController.beginWandering()
            } else {
                windowController.stopWandering()
            }
        }
        .onChange(of: store.wanderingEnabled) { enabled in
            if enabled == false { windowController.stopWandering() }
        }
        .onReceive(lifestyleTimer) { _ in
            var transaction = Transaction()
            transaction.disablesAnimations = true
            withTransaction(transaction) { store.lifestyleTick() }
        }
        .onReceive(activityAnimationTimer) { _ in
            guard store.activity != .idle, store.activity != .walking else { return }
            store.advanceActivityFrame()
        }
        .onReceive(walkingTimer) { _ in
            guard store.activity == .walking else { return }
            switch windowController.advanceWandering(speed: store.wanderSpeed, timeStep: 1.0 / 60.0) {
            case .moving(let direction, let distance):
                store.updateWalkingDirection(direction)
                store.advanceWalkingFrame(distance: Double(distance))
            case .arrived:
                store.finishWalking()
            case .paused:
                break
            }
        }
        .onReceive(llmTimer) { _ in
            store.automaticThoughtTick()
        }
    }

    private var activityFrameOffset: CGSize {
        store.activity.alignmentOffset(for: store.animationFrame)
    }

    private var characterHorizontalScale: CGFloat {
        store.activity.horizontalScale(walkingDirection: store.walkingDirection)
    }

    private var dragGesture: some Gesture {
        DragGesture(minimumDistance: 1, coordinateSpace: .local)
            .onChanged { _ in
                if didDrag == false {
                    didDrag = true
                    windowController.beginDrag()
                }
                if store.pose != .grabbed {
                    store.beginDrag()
                }
                windowController.dragWindowToCurrentMouse()
            }
            .onEnded { _ in
                windowController.endDrag()
                store.endDrag()
                withAnimation(.spring(response: 0.16, dampingFraction: 0.72)) {
                    dropBounce = 11
                }
                withAnimation(.spring(response: 0.18, dampingFraction: 0.7)) {
                    dropBounce = 0
                }
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.03) { didDrag = false }
            }
    }

    @ViewBuilder
    private var contextMenu: some View {
        Button(store.showsBubble ? "隐藏气泡" : "显示气泡") { store.toggleBubble() }
        Button(store.wanderingEnabled ? "暂停自由活动" : "恢复自由活动") {
            store.setWanderingEnabled(store.wanderingEnabled == false)
        }
        Menu("生活动作") {
            Button("去走走") { store.performLifestyle(.walking) }
            Button("喝茶") { store.performLifestyle(.drinkingTea) }
            Button("吃点心") { store.performLifestyle(.eatingSnack) }
        }
        Button("让睦想一句") { store.requestFreshThought() }
        Button("打开控制台…") {
            NSApp.sendAction(Selector(("showSettingsWindow:")), to: nil, from: nil)
        }
        Menu("窗口层级：\(store.layerMode.title)") {
            ForEach(WindowLayerMode.allCases) { mode in
                Button {
                    store.setLayerMode(mode)
                } label: {
                    if store.layerMode == mode {
                        Label(mode.title, systemImage: "checkmark")
                    } else {
                        Text(mode.title)
                    }
                }
            }
        }
        Divider()
        Menu("大小：\(Int((store.scale * 100).rounded()))%") {
            ForEach([0.6, 0.8, 1.0, 1.2, 1.4], id: \.self) { scale in
                Button {
                    store.setScale(scale)
                } label: {
                    let title = "\(Int(scale * 100))%"
                    if abs(store.scale - scale) < 0.01 {
                        Label(title, systemImage: "checkmark")
                    } else {
                        Text(title)
                    }
                }
            }
        }
        Divider()
        Button("退出若叶睦桌宠") { NSApplication.shared.terminate(nil) }
    }
}
