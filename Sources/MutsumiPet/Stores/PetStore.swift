import Foundation

@MainActor
final class PetStore: ObservableObject {
    @Published private(set) var mood: PetMood = .sleepy
    @Published private(set) var pose: PetPose = .idle
    @Published private(set) var message = "……在。"
    @Published var showsBubble = true
    @Published private(set) var layerMode: WindowLayerMode
    @Published private(set) var scale: Double
    @Published private(set) var activity: PetActivity = .idle
    @Published private(set) var walkingDirection = -1
    @Published private(set) var wanderingEnabled: Bool
    @Published private(set) var wanderSpeed: Double
    @Published private(set) var animationFrame = 0

    private var phraseIndex = 0
    private var dismissTask: Task<Void, Never>?
    private var settleTask: Task<Void, Never>?
    private let defaults: UserDefaults
    private weak var llmSettings: LLMSettingsStore?
    private let llmClient = LLMDialogueClient()
    private var llmTask: Task<Void, Never>?
    private var activityTask: Task<Void, Never>?
    private var animationSequenceIndex = 0
    private var walkingDistance: Double = 0

    private static let scaleKey = "pet.scale"
    private static let layerModeKey = "pet.windowLayerMode"
    private static let wanderingEnabledKey = "pet.wanderingEnabled"
    private static let wanderSpeedKey = "pet.wanderSpeed"

    static let phrases = [
        "……在。",
        "你叫我？",
        "不说话，也可以待在一起。",
        "累了就休息。",
        "今天的黄瓜……长高了一点。",
        "吉他，之后会练。",
        "桌面有点乱。",
        "我不是不在意。"
    ]

    init(defaults: UserDefaults = .standard, llmSettings: LLMSettingsStore? = nil) {
        self.defaults = defaults
        self.llmSettings = llmSettings
        let savedScale = defaults.object(forKey: Self.scaleKey) as? Double ?? 1
        scale = min(max(savedScale, 0.6), 1.4)
        layerMode = WindowLayerMode(
            rawValue: defaults.string(forKey: Self.layerModeKey) ?? ""
        ) ?? .front
        wanderingEnabled = defaults.object(forKey: Self.wanderingEnabledKey) as? Bool ?? true
        let savedSpeed = defaults.object(forKey: Self.wanderSpeedKey) as? Double ?? 42
        wanderSpeed = min(max(savedSpeed, 18), 90)
    }

    func reactToTap() {
        interruptActivity()
        phraseIndex = (phraseIndex + 1) % Self.phrases.count
        message = Self.phrases[phraseIndex]
        mood = PetMood.allCases[(mood.rawValue + 1) % PetMood.allCases.count]
        pose = switch mood {
        case .sleepy: .idle
        case .curious: .curious
        case .pleased: .happy
        }
        showsBubble = true
        scheduleDismiss()
    }

    func idleTick(randomIndex: Int? = nil) {
        guard showsBubble == false else { return }
        let index = randomIndex ?? Int.random(in: 0..<Self.phrases.count)
        phraseIndex = max(0, min(index, Self.phrases.count - 1))
        message = Self.phrases[phraseIndex]
        mood = .sleepy
        pose = phraseIndex.isMultiple(of: 2) ? .sleeping : .idle
        showsBubble = true
        scheduleDismiss()
    }

    func beginDrag() {
        guard pose != .grabbed else { return }
        interruptActivity()
        settleTask?.cancel()
        dismissTask?.cancel()
        pose = .grabbed
        mood = .curious
        message = "……被抓住了。"
        showsBubble = true
    }

    func endDrag() {
        guard pose == .grabbed else { return }
        pose = .curious
        message = "……放下来了。"
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

    func lifestyleTick(randomEvent: Int? = nil) {
        guard wanderingEnabled, pose != .grabbed, activity == .idle else { return }
        switch randomEvent ?? Int.random(in: 0..<8) {
        case 0, 1, 2, 3:
            performLifestyle(.walking)
        case 4:
            performLifestyle(.drinkingTea)
        case 5:
            performLifestyle(.eatingSnack)
        case 6:
            pose = .sleeping
            mood = .sleepy
            message = "稍微……休息一下。"
            showsBubble = true
            scheduleDismiss()
        default:
            pose = .curious
            mood = .curious
            message = "那边有什么？"
            showsBubble = true
            scheduleDismiss()
        }
    }

    func performLifestyle(_ requestedActivity: PetActivity) {
        guard wanderingEnabled, pose != .grabbed else { return }
        interruptActivity()
        switch requestedActivity {
        case .walking:
            // Arrival normally ends walking; this timeout is only a safety net
            // for a window that cannot move or disappears mid-activity.
            beginActivity(.walking, duration: .seconds(20))
            pose = .curious
            mood = .curious
            showsBubble = false
        case .drinkingTea:
            beginActivity(.drinkingTea, duration: .seconds(7))
            pose = .idle
            mood = .pleased
            message = "茶……温度刚好。"
            showsBubble = true
            scheduleDismiss()
        case .eatingSnack:
            beginActivity(.eatingSnack, duration: .seconds(7))
            pose = .happy
            mood = .pleased
            message = "点心，分你一点。"
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
    }

    func requestFreshThought() {
        message = "……"
        mood = .curious
        pose = .curious
        showsBubble = true
        scheduleDismiss()
        requestDynamicReply(event: "用户请你主动说一句此刻想到的话。")
    }

    func automaticThoughtTick() {
        guard llmSettings?.automaticThoughtsEnabled == true else { return }
        requestDynamicReply(event: "你在桌面安静待了一会儿。自然地对用户说一句此刻想到的话。")
    }

    private func requestDynamicReply(event: String) {
        guard let configuration = llmSettings?.configuration() else {
            llmSettings?.noteConfigurationUnavailable()
            return
        }
        let settings = llmSettings
        settings?.noteRequestBegan()
        llmTask?.cancel()
        llmTask = Task { @MainActor [weak self, weak settings, llmClient] in
            let clock = ContinuousClock()
            let start = clock.now
            do {
                let reply = try await llmClient.reply(configuration: configuration, event: event)
                guard Task.isCancelled == false, let self else { return }
                settings?.noteRequestSucceeded(reply: reply, latency: start.duration(to: clock.now))
                self.message = reply
                self.showsBubble = true
                self.scheduleDismiss()
            } catch {
                guard Task.isCancelled == false else { return }
                settings?.noteRequestFailed(error, latency: start.duration(to: clock.now))
                // Local dialogue remains visible; network failures never block pet interaction.
            }
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
