import Foundation

struct LLMConfiguration: Sendable, Equatable {
    let endpoint: URL
    let model: String
    let apiKey: String
    let reasoningEffort: String
    let timeoutSeconds: Double
    let systemPrompt: String
}

enum LLMConnectionState: Equatable {
    case disabled
    case missingKey
    case ready
    case error(String)

    var label: String {
        switch self {
        case .disabled: "已停用"
        case .missingKey: "尚未保存密钥"
        case .ready: "已配置"
        case .error(let message): message
        }
    }
}

enum LLMRequestState: Equatable {
    case idle
    case requesting
    case succeeded
    case failed(String)

    var label: String {
        switch self {
        case .idle: "尚未请求"
        case .requesting: "正在请求 Luna Low…"
        case .succeeded: "最近一次请求成功"
        case .failed(let message): "请求失败：\(message)"
        }
    }
}
