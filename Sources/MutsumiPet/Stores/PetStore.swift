import Foundation

@MainActor
final class PetStore: ObservableObject {
    @Published private(set) var mood: PetMood = .sleepy
    @Published private(set) var pose: PetPose = .idle
    @Published private(set) var message = PetDialogueScene.idle.lines[0]
    @Published var showsBubble = true
    @Published private(set) var layerMode: WindowLayerMode
    @Published private(set) var scale: Double
    @Published private(set) var activity: PetActivity = .idle
    @Published private(set) var walkingDirection = -1
    @Published private(set) var wanderingEnabled: Bool
    @Published private(set) var wanderSpeed: Double
    @Published private(set) var animationFrame = 0

    private var dismissTask: Task<Void, Never>?
    private var settleTask: Task<Void, Never>?
    private let defaults: UserDefaults
    private var activityTask: Task<Void, Never>?
    private var animationSequenceIndex = 0
    private var walkingDistance: Double = 0

    private static let scaleKey = "pet.scale"
    private static let layerModeKey = "pet.windowLayerMode"
    private static let wanderingEnabledKey = "pet.wanderingEnabled"
    private static let wanderSpeedKey = "pet.wanderSpeed"

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        let savedScale = defaults.object(forKey: Self.scaleKey) as? Double ?? 1
        scale = min(max(savedScale, 0.6), 1.4)
        layerMode = WindowLayerMode(
            rawValue: defaults.string(forKey: Self.layerModeKey) ?? ""
        ) ?? .front
        wanderingEnabled = defaults.object(forKey: Self.wanderingEnabledKey) as? Bool ?? true
        let savedSpeed = defaults.object(forKey: Self.wanderSpeedKey) as? Double ?? 42
        wanderSpeed = min(max(savedSpeed, 18), 90)
    }

    func reactToTap(randomDialogueIndex: Int? = nil) {
        interruptActivity()
        mood = PetMood.allCases[(mood.rawValue + 1) % PetMood.allCases.count]
        pose = switch mood {
        case .sleepy: .idle
        case .curious: .curious
        case .pleased: .happy
        }
        message = dialogueScene(for: pose).line(at: randomDialogueIndex)
        showsBubble = true
        scheduleDismiss()
    }

    func idleTick(randomIndex: Int? = nil, sleeping: Bool? = nil) {
        guard showsBubble == false else { return }
        let usesSleepingPose = sleeping ?? Bool.random()
        let scene: PetDialogueScene = usesSleepingPose ? .sleeping : .idle
        message = scene.line(at: randomIndex)
        mood = .sleepy
        pose = usesSleepingPose ? .sleeping : .idle
        showsBubble = true
        scheduleDismiss()
    }

    func beginDrag(randomDialogueIndex: Int? = nil) {
        guard pose != .grabbed else { return }
        interruptActivity()
        settleTask?.cancel()
        dismissTask?.cancel()
        pose = .grabbed
        mood = .curious
        message = PetDialogueScene.grabbed.line(at: randomDialogueIndex)
        showsBubble = true
    }

    func endDrag(randomDialogueIndex: Int? = nil) {
        guard pose == .grabbed else { return }
        pose = .curious
        message = PetDialogueScene.released.line(at: randomDialogueIndex)
        scheduleDismiss()
        settleTask?.cancel()
        settleTask = Task { @MainActor [weak self] in
            try? await Task.sleep(for: .milliseconds(120))
            guard Task.isCancelled == false, self?.pose != .grabbed else { return }
            self?.pose = .idle
            self?.mood = .sleepy
        }
    }

    func toggleBubble() {
        showsBubble.toggle()
        if showsBubble { scheduleDismiss() }
    }

    func setScale(_ newScale: Double) {
        scale = min(max(newScale, 0.6), 1.4)
        defaults.set(scale, forKey: Self.scaleKey)
    }

    func setLayerMode(_ mode: WindowLayerMode) {
        layerMode = mode
        defaults.set(mode.rawValue, forKey: Self.layerModeKey)
    }

    func setWanderingEnabled(_ enabled: Bool) {
        wanderingEnabled = enabled
        defaults.set(enabled, forKey: Self.wanderingEnabledKey)
        if enabled == false { interruptActivity() }
    }

    func setWanderSpeed(_ speed: Double) {
        wanderSpeed = min(max(speed, 18), 90)
        defaults.set(wanderSpeed, forKey: Self.wanderSpeedKey)
    }

    func lifestyleTick(randomEvent: Int? = nil, randomDialogueIndex: Int? = nil) {
        guard wanderingEnabled, pose != .grabbed, activity == .idle else { return }
        switch randomEvent ?? Int.random(in: 0..<8) {
        case 0, 1, 2, 3:
            performLifestyle(.walking, randomDialogueIndex: randomDialogueIndex)
        case 4:
            performLifestyle(.drinkingTea, randomDialogueIndex: randomDialogueIndex)
        case 5:
            performLifestyle(.eatingSnack, randomDialogueIndex: randomDialogueIndex)
        case 6:
            pose = .sleeping
            mood = .sleepy
            message = PetDialogueScene.sleeping.line(at: randomDialogueIndex)
            showsBubble = true
            scheduleDismiss()
        default:
            pose = .curious
            mood = .curious
            message = PetDialogueScene.curious.line(at: randomDialogueIndex)
            showsBubble = true
            scheduleDismiss()
        }
    }

    func performLifestyle(_ requestedActivity: PetActivity, randomDialogueIndex: Int? = nil) {
        guard wanderingEnabled, pose != .grabbed else { return }
        interruptActivity()
        switch requestedActivity {
        case .walking:
            // Arrival normally ends walking; this timeout is only a safety net
            // for a window that cannot move or disappears mid-activity.
            beginActivity(.walking, duration: .seconds(20))
            pose = .curious
            mood = .curious
            message = PetDialogueScene.walking.line(at: randomDialogueIndex)
            showsBubble = true
            scheduleDismiss()
        case .drinkingTea:
            beginActivity(.drinkingTea, duration: .seconds(7))
            pose = .idle
            mood = .pleased
            message = PetDialogueScene.drinkingTea.line(at: randomDialogueIndex)
            showsBubble = true
            scheduleDismiss()
        case .eatingSnack:
            beginActivity(.eatingSnack, duration: .seconds(7))
            pose = .happy
            mood = .pleased
            message = PetDialogueScene.eatingSnack.line(at: randomDialogueIndex)
            showsBubble = true
            scheduleDismiss()
        case .idle:
            break
        }
    }

    func updateWalkingDirection(_ direction: Int) {
        walkingDirection = direction < 0 ? -1 : 1
    }

    func advanceActivityFrame() {
        guard activity != .idle else { return }
        let sequence = activity.frameSequence
        animationSequenceIndex = (animationSequenceIndex + 1) % sequence.count
        animationFrame = sequence[animationSequenceIndex]
    }

    func advanceWalkingFrame(distance: Double) {
        guard activity == .walking, distance > 0 else { return }
        walkingDistance += distance
        let strideLength = 44 * scale
        let phase = walkingDistance.truncatingRemainder(dividingBy: strideLength) / strideLength
        animationFrame = min(Int(phase * 4), 3)
    }

    func finishWalking() {
        guard activity == .walking else { return }
        interruptActivity()
        pose = .idle
        mood = .sleepy
        message = PetDialogueScene.idle.line()
        showsBubble = true
        scheduleDismiss()
    }

    func speakForCurrentState(randomDialogueIndex: Int? = nil) {
        message = currentDialogueScene.line(at: randomDialogueIndex)
        showsBubble = true
        scheduleDismiss()
    }

    private var currentDialogueScene: PetDialogueScene {
        switch activity {
        case .walking: .walking
        case .drinkingTea: .drinkingTea
        case .eatingSnack: .eatingSnack
        case .idle: dialogueScene(for: pose)
        }
    }

    private func dialogueScene(for pose: PetPose) -> PetDialogueScene {
        switch pose {
        case .idle: .idle
        case .curious: .curious
        case .happy: .happy
        case .sleeping: .sleeping
        case .grabbed: .grabbed
        }
    }

    private func scheduleDismiss() {
        dismissTask?.cancel()
        dismissTask = Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(6))
            guard Task.isCancelled == false else { return }
            self?.showsBubble = false
            self?.mood = .sleepy
            if self?.pose != .grabbed { self?.pose = .idle }
        }
    }

    private func beginActivity(_ newActivity: PetActivity, duration: Duration) {
        activityTask?.cancel()
        activity = newActivity
        animationSequenceIndex = 0
        walkingDistance = 0
        animationFrame = newActivity.frameSequence[0]
        activityTask = Task { @MainActor [weak self] in
            try? await Task.sleep(for: duration)
            guard Task.isCancelled == false, let self, self.pose != .grabbed else { return }
            self.activity = .idle
            self.pose = .idle
            self.mood = .sleepy
        }
    }

    private func interruptActivity() {
        activityTask?.cancel()
        activityTask = nil
        activity = .idle
        animationSequenceIndex = 0
        walkingDistance = 0
        animationFrame = 0
    }
}
