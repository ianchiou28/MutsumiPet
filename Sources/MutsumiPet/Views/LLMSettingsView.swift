import SwiftUI

struct LLMSettingsView: View {
    @ObservedObject var settings: LLMSettingsStore
    @ObservedObject var petStore: PetStore
    @State private var apiKey = ""

    var body: some View {
        TabView {
            connectionTab
                .tabItem { Label("连接", systemImage: "network") }
            personalityTab
                .tabItem { Label("角色", systemImage: "text.bubble") }
            petTab
                .tabItem { Label("桌宠", systemImage: "sparkles") }
            activityTab
                .tabItem { Label("状态", systemImage: "waveform.path.ecg") }
        }
        .padding(18)
        .frame(width: 620, height: 470)
    }

    private var connectionTab: some View {
        Form {
            Section("语言模型") {
                Toggle("启用动态回复", isOn: $settings.isEnabled)
                TextField("API 地址", text: $settings.endpointString)
                TextField("模型 ID", text: $settings.model)

                Picker("运行档位", selection: $settings.reasoningEffort) {
                    Text("Luna Low（最快）").tag("low")
                    Text("Medium").tag("medium")
                    Text("High").tag("high")
                }
                .pickerStyle(.segmented)

                HStack {
                    Text("超时")
                    Slider(value: $settings.timeoutSeconds, in: 3...30, step: 1)
                    Text("\(Int(settings.timeoutSeconds)) 秒")
                        .monospacedDigit()
                        .frame(width: 48, alignment: .trailing)
                }

                SecureField("新密钥（留空则保持钥匙串中的密钥）", text: $apiKey)
            }

            Section {
                HStack {
                    statusDot(color: connectionColor)
                    Text(settings.state.label)
                        .foregroundStyle(.secondary)
                    Spacer()
                    Button("保存") { save() }
                    Button("保存并测试") {
                        save()
                        Task { await settings.testConnection() }
                    }
                    .keyboardShortcut(.defaultAction)
                    .disabled(settings.requestState == .requesting)
                }
            }

            Text("接口模型名保持 gpt-5.6-luna；Luna Low 通过 low 推理档位发送，因此更快。密钥只保存在 macOS 钥匙串。")
                .font(.footnote)
                .foregroundStyle(.secondary)
        }
        .formStyle(.grouped)
    }

    private var personalityTab: some View {
        Form {
            Section("触发方式") {
                Toggle("每分钟自动让睦想一句", isOn: $settings.automaticThoughtsEnabled)
                Text("左键点击始终使用即时预设台词，不调用网络。右键选择“让睦想一句”仍可手动生成。")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }

            Section("角色提示词") {
                TextEditor(text: $settings.systemPrompt)
                    .font(.body)
                    .frame(minHeight: 190)
                HStack {
                    Button("恢复默认") { settings.systemPrompt = LLMSettingsStore.defaultSystemPrompt }
                    Spacer()
                    Button("保存角色设定") { save() }
                        .keyboardShortcut(.defaultAction)
                }
            }
        }
        .formStyle(.grouped)
    }

    private var petTab: some View {
        Form {
            Section("外观") {
                HStack {
                    Text("大小")
                    Slider(
                        value: Binding(
                            get: { petStore.scale },
                            set: { petStore.setScale($0) }
                        ),
                        in: 0.6...1.4,
                        step: 0.05
                    )
                    Text("\(Int((petStore.scale * 100).rounded()))%")
                        .monospacedDigit()
                        .frame(width: 48, alignment: .trailing)
                }
                Toggle("显示对话气泡", isOn: $petStore.showsBubble)
            }

            Section("自由活动") {
                Toggle(
                    "允许散步、喝茶和吃点心",
                    isOn: Binding(
                        get: { petStore.wanderingEnabled },
                        set: { petStore.setWanderingEnabled($0) }
                    )
                )
                HStack {
                    Text("散步速度")
                    Slider(
                        value: Binding(
                            get: { petStore.wanderSpeed },
                            set: { petStore.setWanderSpeed($0) }
                        ),
                        in: 18...90,
                        step: 3
                    )
                    Text("\(Int(petStore.wanderSpeed))")
                        .monospacedDigit()
                        .frame(width: 32, alignment: .trailing)
                }
                Text("她会随机选择散步、休息、喝茶或吃点心；拖拽时会立即停止移动。")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }

            Section("窗口") {
                Picker(
                    "窗口层级",
                    selection: Binding(
                        get: { petStore.layerMode },
                        set: { petStore.setLayerMode($0) }
                    )
                ) {
                    ForEach(WindowLayerMode.allCases) { mode in
                        Text(mode.title).tag(mode)
                    }
                }
                Text("“桌面”会把她放到普通应用窗口后面；“最前”会始终保持可见。")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }
        }
        .formStyle(.grouped)
    }

    private var activityTab: some View {
        Form {
            Section("最近一次 LLM 请求") {
                HStack(alignment: .top) {
                    statusDot(color: requestColor)
                    Text(settings.requestState.label)
                        .textSelection(.enabled)
                }
                if let date = settings.lastRequestAt {
                    LabeledContent("时间", value: date.formatted(date: .omitted, time: .standard))
                }
                if let latency = settings.lastLatencyMilliseconds {
                    LabeledContent("耗时", value: "\(latency) ms")
                }
                if settings.lastReply.isEmpty == false {
                    LabeledContent("回复") {
                        Text(settings.lastReply)
                            .textSelection(.enabled)
                    }
                }
            }

            Section("诊断") {
                Button("重新测试连接") {
                    Task { await settings.testConnection() }
                }
                .disabled(settings.requestState == .requesting)
                Text("如果这里显示 HTTP 502/503，代表密钥与配置已送达网关，但模型服务暂时不可用；桌宠会立即保留本地台词。")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }
        }
        .formStyle(.grouped)
    }

    private func save() {
        settings.save(apiKey: apiKey)
        apiKey = ""
    }

    private func statusDot(color: Color) -> some View {
        Circle()
            .fill(color)
            .frame(width: 9, height: 9)
            .padding(.top, 4)
    }

    private var connectionColor: Color {
        switch settings.state {
        case .ready: .green
        case .disabled: .secondary
        case .missingKey: .orange
        case .error: .red
        }
    }

    private var requestColor: Color {
        switch settings.requestState {
        case .idle: .secondary
        case .requesting: .blue
        case .succeeded: .green
        case .failed: .red
        }
    }
}
