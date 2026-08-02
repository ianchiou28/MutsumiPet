# MutsumiPet · 若叶睦桌宠

<p align="center">
  <img src="assets/MutsumiPetIcon.png" width="420" alt="马赛克风格的若叶睦应用图标">
</p>

一个透明桌宠。macOS 版使用 SwiftUI，Windows 版使用 WPF，两者共用同一套素材、动作与台词。

## 下载

- [macOS 版](https://github.com/ianchiou28/MutsumiPet/releases/latest/download/MutsumiPet-macOS.zip) · 需要 macOS 13 或更高版本
- [Windows 版](https://github.com/ianchiou28/MutsumiPet/releases/latest/download/MutsumiPet-Windows.exe) · 需要 Windows 10 或更高版本，单文件免安装

Windows 版基于系统自带的 .NET Framework 4.8，下载后双击即可运行，不需要安装任何运行时。

> Windows 版没有做代码签名，首次运行时 SmartScreen 会提示「未知发布者」，
> 点击「更多信息 → 仍要运行」即可。若系统开启了智能应用控制（Smart App Control），
> 程序会被直接拦截——原因与取舍见[代码签名](#代码签名)一节。

应用图标采用马赛克瓷砖拼成的 Q 版若叶睦设计，并包含完整的 macOS 多尺寸 ICNS 资源与 Windows 多尺寸 ICO 资源。

## 动作预览

### 日常表情与互动

| 待机 | 疑惑 | 开心 |
|:---:|:---:|:---:|
| <img src="Sources/MutsumiPet/Resources/mutsumi_pet.png" width="190" alt="若叶睦待机动作"> | <img src="Sources/MutsumiPet/Resources/mutsumi_curious.png" width="190" alt="若叶睦疑惑动作"> | <img src="Sources/MutsumiPet/Resources/mutsumi_happy.png" width="190" alt="若叶睦开心动作"> |

| 睡觉 | 被抓起来 |
|:---:|:---:|
| <img src="Sources/MutsumiPet/Resources/mutsumi_sleeping.png" width="190" alt="若叶睦睡觉动作"> | <img src="Sources/MutsumiPet/Resources/mutsumi_grabbed.png" width="190" alt="若叶睦被抓起来的动作"> |

### 生活动作帧

**走路**

<img src="Sources/MutsumiPet/Resources/mutsumi_walk_strip.png" width="820" alt="若叶睦走路动作帧">

**喝茶**

<img src="Sources/MutsumiPet/Resources/mutsumi_tea_strip.png" width="820" alt="若叶睦喝茶动作帧">

**吃点心**

<img src="Sources/MutsumiPet/Resources/mutsumi_snack_strip.png" width="820" alt="若叶睦吃点心动作帧">

## 使用

以下行为两个平台完全一致：

- 点击若叶睦：切换表情与气泡。
- 拖动角色：睦会被拎起、悬空晃动，松手后软着陆。
- 待机时会在坐姿、疑惑、挥手与睡姿之间切换。
- 右键角色：切换置顶、气泡、尺寸或退出。
- 右键可选 60%–140% 尺寸，并在始终置顶、普通窗口、桌面后台之间切换。
- 自动换动作采用无过渡的离散切换，减少延迟和残影。
- 会在当前屏幕可用区域内自由散步，并随机喝茶、吃点心或停下来休息；三种生活动作均使用独立生成的四帧透明动画。
- 散步采用 60 Hz 平滑位移和距离驱动步态：每走过四分之一步幅才切换姿势，速度改变时步频会同步变化；角色抵达一次随机短距离目标后会立即恢复默认动作，拖拽也会立即打断移动。
- 每种动作都有 5–8 句专属预设台词，触发动作时会从对应台词组随机显示。
- 右键选择“让睦说一句”，会根据她当前的动作或表情立即说出对应台词。
- `Command-M`：互动；`Command-B`：显示或隐藏气泡（Windows 见下）。

运行（macOS）：

```bash
./script/build_and_run.sh
```

要求 macOS 13 或更高版本。

## Windows 版

Windows 版逐条复刻了上面的全部行为——同样的 380×450 舞台、314pt 角色框、四帧生活动作、
按距离驱动的步态、6 秒气泡计时与全部预设台词。以下是平台差异：

- **窗口层级**：「始终置顶」对应 `HWND_TOPMOST`；「位于桌面后台」把窗口钉在 z 序最底层，
  始终位于其他窗口之下、壁纸之上。
- **不抢焦点**：窗口带 `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`，点击睦不会打断你正在输入的应用，
  任务栏和 Alt+Tab 中也不会出现（对应 macOS 的 accessory 策略）。
- **快捷键**：macOS 的菜单栏在 Windows 上没有对应物，改用全局热键
  `Ctrl+Alt+M` 互动、`Ctrl+Alt+B` 显示或隐藏气泡、`Ctrl+Alt+Q` 退出。
  若组合键已被其他程序占用则自动跳过，右键菜单始终可用。
- **偏好保存**：写入 `%APPDATA%\MutsumiPet\settings.ini`（对应 macOS 的 `UserDefaults`）。
- **高 DPI**：位置与步幅全部以设备无关单位计算，仅在调用 `SetWindowPos` 时换算成物理像素，
  因此在 100%–200% 等各种缩放下，走路速度与体型都保持一致。应用使用 WPF 默认的系统级 DPI 感知，
  多显示器且各屏缩放不同时，副屏上会按主屏比例呈现。

构建并运行：

```powershell
./script/build_windows.ps1 -Run
```

脚本直接调用 Windows 自带的 .NET Framework 4.8 编译器，**无需安装 SDK**；素材会被嵌入
可执行文件，产物是 `build/windows/MutsumiPet.exe` 单文件。

> 若 Windows 拒绝启动自行编译的 `MutsumiPet.exe`，通常是智能应用控制（Smart App Control）
> 拦截了未签名程序。可在「设置 → 隐私和安全性 → Windows 安全中心 → 应用和浏览器控制」中关闭，
> 或对可执行文件签名（见下）。**注意：智能应用控制一旦关闭就无法再打开，除非重装 Windows。**

### 代码签名

未签名的 exe 会被 SmartScreen 警告、被智能应用控制直接拦截。构建脚本内置了签名步骤，
拿到证书后加一个参数即可：

```powershell
# 证书装在 Cert:\CurrentUser\My（推荐，密钥不会出现在命令行里）
./script/build_windows.ps1 -SignThumbprint <指纹>

# 或者用 .pfx 文件，密码从环境变量读取，脚本不会记录它
$env:MUTSUMIPET_CERT_PASSWORD = '...'
./script/build_windows.ps1 -SignCertificate .\cert.pfx
```

脚本优先调用 Windows SDK 的 `signtool`（RFC 3161 时间戳），找不到就退回内置的
`Set-AuthenticodeSignature`。两条路径都会打时间戳，这样证书过期后已发布的版本仍然有效。

证书本身要另外购买，各选项差别很大：

| 方案 | 成本 | 效果 |
|---|---|---|
| 自签名 | 免费 | **仅用于自测签名流程**，对其他用户毫无作用 |
| [Azure Trusted Signing](https://learn.microsoft.com/azure/trusted-signing/) | 约 $10/月 | 微软自营，不需要硬件 U 盾，可直接在 GitHub Actions 里签；个人开发者也能申请（需要可验证的 3 年以上身份记录）|
| [SignPath Foundation](https://signpath.org/) | 免费 | 面向符合条件的开源项目 |
| Certum 开源代码签名 | 约 €100 起 | 支持个人身份验证，需要硬件卡 |
| 商业 OV / EV 证书 | 约 $200–600/年 | 2023 年起私钥必须存放在硬件或云 HSM 中；EV 可立即获得 SmartScreen 信誉 |

对这个非商业同人项目来说，Azure Trusted Signing 或 SignPath 是性价比最合理的两个选择。

需要说明的是：签名能解决 SmartScreen 的「未知发布者」警告，但**智能应用控制比 SmartScreen 更严格**，
它同时要求微软的信誉图谱认可该程序。有效签名会大幅提高通过率，但不保证首日即被放行。

## 开发与测试

macOS：

```bash
swift test
./script/build_and_run.sh --verify
```

Windows：

```powershell
./script/build_windows.ps1 -Test
```

`-Verify` 会在跑完测试后启动应用并确认进程存活，等价于 `build_and_run.sh --verify`。
若已安装 .NET SDK，也可以用 MSBuild 走同一份源码：

```powershell
dotnet build windows/MutsumiPet.Windows/MutsumiPet.Windows.csproj -c Release
dotnet run --project windows/MutsumiPet.Windows.Tests -c Release
```

macOS 端使用 Swift Package Manager，Windows 端只依赖系统自带的 WPF；两端都没有第三方运行时依赖，
所有动作与台词均完全离线运行。

## 许可证与声明

程序源码采用 [MIT License](LICENSE)。若叶睦角色及相关衍生图像素材不包含在 MIT 授权范围内，详见 [ASSET_NOTICE.md](ASSET_NOTICE.md)。本项目是非官方粉丝项目，与角色相关权利方无隶属或背书关系。
