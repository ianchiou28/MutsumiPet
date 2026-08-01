import Foundation

actor LLMDialogueClient {
    private let session: URLSession

    init() {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 8
        configuration.timeoutIntervalForResource = 10
        configuration.requestCachePolicy = .reloadIgnoringLocalAndRemoteCacheData
        session = URLSession(configuration: configuration)
    }

    func reply(configuration: LLMConfiguration, event: String) async throws -> String {
        do {
            return try await chatCompletion(configuration: configuration, event: event)
        } catch let error as LLMServiceError where error.shouldTryResponsesAPI {
            return try await responseAPI(configuration: configuration, event: event)
        }
    }

    private func chatCompletion(configuration: LLMConfiguration, event: String) async throws -> String {
        let url = endpoint(base: configuration.endpoint, path: "chat/completions")
        let body: [String: Any] = [
            "model": configuration.model,
            "reasoning_effort": configuration.reasoningEffort,
            "max_completion_tokens": 80,
            "messages": [
                ["role": "system", "content": configuration.systemPrompt],
                ["role": "user", "content": event]
            ]
        ]
        let json = try await perform(url: url, configuration: configuration, body: body)
        guard let choices = json["choices"] as? [[String: Any]],
              let message = choices.first?["message"] as? [String: Any],
              let content = message["content"] as? String else {
            throw LLMServiceError.invalidResponse
        }
        return sanitize(content)
    }

    private func responseAPI(configuration: LLMConfiguration, event: String) async throws -> String {
        let url = endpoint(base: configuration.endpoint, path: "responses")
        let body: [String: Any] = [
            "model": configuration.model,
            "instructions": configuration.systemPrompt,
            "input": event,
            "reasoning": ["effort": configuration.reasoningEffort],
            "max_output_tokens": 80
        ]
        let json = try await perform(url: url, configuration: configuration, body: body)

        if let outputText = json["output_text"] as? String {
            return sanitize(outputText)
        }
        if let output = json["output"] as? [[String: Any]] {
            for item in output {
                guard let content = item["content"] as? [[String: Any]] else { continue }
                for block in content where block["type"] as? String == "output_text" {
                    if let text = block["text"] as? String { return sanitize(text) }
                }
            }
        }
        throw LLMServiceError.invalidResponse
    }

    private func perform(
        url: URL,
        configuration: LLMConfiguration,
        body: [String: Any]
    ) async throws -> [String: Any] {
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("Bearer \(configuration.apiKey)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.timeoutInterval = configuration.timeoutSeconds
        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (data, response) = try await session.data(for: request)
        guard let http = response as? HTTPURLResponse else {
            throw LLMServiceError.invalidResponse
        }
        guard (200..<300).contains(http.statusCode) else {
            let serverMessage = Self.serverErrorMessage(from: data)
            throw LLMServiceError.httpStatus(http.statusCode, serverMessage)
        }
        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw LLMServiceError.invalidResponse
        }
        return json
    }

    private func endpoint(base: URL, path: String) -> URL {
        var url = base
        if url.path.hasSuffix("/v1") == false {
            url.appendPathComponent("v1")
        }
        url.appendPathComponent(path)
        return url
    }

    private func sanitize(_ raw: String) -> String {
        let firstLine = raw
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .split(whereSeparator: \.isNewline)
            .first
            .map(String.init) ?? ""
        let cleaned = firstLine.trimmingCharacters(in: CharacterSet(charactersIn: "\"'「」『』"))
        guard cleaned.isEmpty == false else { return "……" }
        return String(cleaned.prefix(42))
    }

    private static func serverErrorMessage(from data: Data) -> String? {
        guard let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let error = json["error"] as? [String: Any] else { return nil }
        return error["message"] as? String
    }
}

enum LLMServiceError: LocalizedError, Equatable {
    case httpStatus(Int, String?)
    case invalidResponse

    var shouldTryResponsesAPI: Bool {
        switch self {
        case .httpStatus(let code, _): code == 404 || code == 405
        case .invalidResponse: true
        }
    }

    var errorDescription: String? {
        switch self {
        case .httpStatus(let code, let message):
            if let message, message.isEmpty == false { return "HTTP \(code) · \(message)" }
            return "HTTP \(code)"
        case .invalidResponse:
            return "返回内容格式不正确"
        }
    }
}
