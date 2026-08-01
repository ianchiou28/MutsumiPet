import Foundation

@MainActor
final class LLMSettingsStore: ObservableObject {
    @Published var endpointString: String
    @Published var model: String
    @Published var reasoningEffort: String
    @Published var timeoutSeconds: Double
    @Published var systemPrompt: String
    @Published var isEnabled: Bool
    @Published var automaticThoughtsEnabled: Bool
    @Published private(set) var state: LLMConnectionState = .missingKey
    @Published private(set) var requestState: LLMRequestState = .idle
    @Published private(set) var lastReply = ""
    @Published private(set) var lastRequestAt: Date?
    @Published private(set) var lastLatencyMilliseconds: Int?

    private let defaults: UserDefaults
    private var cachedAPIKey: String?
    private let testClient = LLMDialogueClient()

    private static let endpointKey = "llm.endpoint"
    private static let modelKey = "llm.model"
    private static let reasoningEffortKey = "llm.reasoningEffort"
    private static let timeoutKey = "llm.timeoutSeconds"
    private static let systemPromptKey = "llm.systemPrompt"
    private static let enabledKey = "llm.enabled"
    private static let automaticThoughtsKey = "llm.automaticThoughts"

    static let defaultSystemPrompt = """
    你是桌面上的Q版若叶睦，是非官方的同人桌宠。用简体中文回应。
    说话寡言、克制、口拙，但会以自己的方式关心用户；偶尔提到吉他、黄瓜或安静地待在一起。
    每次只输出一句自然短句，最多24个汉字。不要舞台说明、括号动作、引号、自称AI、解释或复述要求。
    """

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        endpointString = defaults.string(forKey: Self.endpointKey) ?? "https://ai-ext.1343263.xyz/"
        model = defaults.string(forKey: Self.modelKey) ?? "gpt-5.6-luna"
        reasoningEffort = defaults.string(forKey: Self.reasoningEffortKey) ?? "low"
        let savedTimeout = defaults.object(forKey: Self.timeoutKey) as? Double ?? 8
        timeoutSeconds = min(max(savedTimeout, 3), 30)
        systemPrompt = defaults.string(forKey: Self.systemPromptKey) ?? Self.defaultSystemPrompt
        isEnabled = defaults.object(forKey: Self.enabledKey) as? Bool ?? true
        automaticThoughtsEnabled = defaults.object(forKey: Self.automaticThoughtsKey) as? Bool ?? true
        refreshKeychainState()
    }

    func refreshKeychainState() {
        do {
            cachedAPIKey = try KeychainStore.loadAPIKey()
            state = isEnabled ? (cachedAPIKey == nil ? .missingKey : .ready) : .disabled
        } catch {
            cachedAPIKey = nil
            state = .error("无法读取钥匙串")
        }
    }

    func save(apiKey: String) {
        let endpoint = endpointString.trimmingCharacters(in: .whitespacesAndNewlines)
        let modelName = model.trimmingCharacters(in: .whitespacesAndNewlines)

        guard let url = URL(string: endpoint), url.scheme == "https", modelName.isEmpty == false else {
            state = .error("请输入有效的 HTTPS 地址和模型名")
            return
        }

        do {
            let newKey = apiKey.trimmingCharacters(in: .whitespacesAndNewlines)
            if newKey.isEmpty == false {
                try KeychainStore.saveAPIKey(newKey)
                cachedAPIKey = newKey
            }
            endpointString = endpoint
            model = modelName
            defaults.set(endpoint, forKey: Self.endpointKey)
            defaults.set(modelName, forKey: Self.modelKey)
            defaults.set(reasoningEffort, forKey: Self.reasoningEffortKey)
            defaults.set(timeoutSeconds, forKey: Self.timeoutKey)
            defaults.set(systemPrompt, forKey: Self.systemPromptKey)
            defaults.set(isEnabled, forKey: Self.enabledKey)
            defaults.set(automaticThoughtsEnabled, forKey: Self.automaticThoughtsKey)
            state = isEnabled ? (cachedAPIKey == nil ? .missingKey : .ready) : .disabled
        } catch {
            state = .error("保存密钥失败")
        }
    }

    func configuration() -> LLMConfiguration? {
        guard isEnabled,
              let apiKey = cachedAPIKey,
              let endpoint = URL(string: endpointString),
              endpoint.scheme == "https",
              model.isEmpty == false else { return nil }
        return LLMConfiguration(
            endpoint: endpoint,
            model: model,
            apiKey: apiKey,
            reasoningEffort: reasoningEffort,
            timeoutSeconds: timeoutSeconds,
            systemPrompt: systemPrompt.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                ? Self.defaultSystemPrompt
                : systemPrompt
        )
    }

    func testConnection() async {
        guard let configuration = configuration() else {
            requestState = .failed("请先启用 LLM 并保存密钥")
            return
        }
        noteRequestBegan()
        let clock = ContinuousClock()
        let start = clock.now
        do {
            let reply = try await testClient.reply(
                configuration: configuration,
                event: "这是一条连接测试。请向用户简短地说你已经在这里。"
            )
            let duration = start.duration(to: clock.now)
            noteRequestSucceeded(reply: reply, latency: duration)
        } catch {
            let duration = start.duration(to: clock.now)
            noteRequestFailed(error, latency: duration)
        }
    }

    func noteRequestBegan() {
        requestState = .requesting
        lastRequestAt = Date()
        lastLatencyMilliseconds = nil
    }

    func noteConfigurationUnavailable() {
        requestState = .failed("请先启用 LLM 并保存密钥")
        lastRequestAt = Date()
        lastLatencyMilliseconds = nil
    }

    func noteRequestSucceeded(reply: String, latency: Duration) {
        requestState = .succeeded
        lastReply = reply
        lastLatencyMilliseconds = Self.milliseconds(latency)
    }

    func noteRequestFailed(_ error: Error, latency: Duration) {
        requestState = .failed(Self.readableError(error))
        lastLatencyMilliseconds = Self.milliseconds(latency)
    }

    private static func milliseconds(_ duration: Duration) -> Int {
        let components = duration.components
        return Int(components.seconds * 1_000 + components.attoseconds / 1_000_000_000_000_000)
    }

    private static func readableError(_ error: Error) -> String {
        if let serviceError = error as? LLMServiceError {
            return serviceError.localizedDescription
        }
        if let urlError = error as? URLError {
            if urlError.code == .timedOut { return "请求超时" }
            return urlError.localizedDescription
        }
        return error.localizedDescription
    }
}
