# WinSentryAI 用户手册

版本：v0.9

## 1. WinSentryAI 是什么

WinSentryAI 是一款可携式 Windows 桌面工具，用于读取 Windows 事件日志，并请 AI 协助生成诊断摘要。它适合已具备基本 Windows 管理知识的 IT 操作人员。

WinSentryAI 不会自动修复系统。AI 输出仅供参考，采取任何操作前都应由人员自行判断。

## 2. 安装需求

- Windows 10 或更新版本
- 支持 Windows 11
- 支持带 Desktop Experience 的 Windows Server
- Windows x64
- Framework-dependent build 需要 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-US/download/dotnet/8.0)
- 建议使用管理员权限，以完整读取事件日志
- 最低实用屏幕 / 窗口尺寸：1280 x 720
- 建议屏幕 / 窗口尺寸：1366 x 768 以上

Server Core 与 Nano Server 不支持，因为 WinSentryAI 是 WPF 桌面应用程序。

建议可携式放置位置：

- `C:\Tools\WinSentryAI\`
- USB 随身盘或可携工具文件夹

不建议放在 `C:\Program Files\`，避免 Windows 写入虚拟化造成设置与数据库路径混乱。

## 3. Build 与运行

请先安装 [.NET 8 SDK](https://dotnet.microsoft.com/en-US/download/dotnet/8.0)，再执行：

```powershell
dotnet build -c Debug -p:Platform=x64
```

固定 build 输出文件夹：

```text
bin\build\
```

从 build 输出运行：

```powershell
.\bin\build\WinSentryAI.exe
```

Framework-dependent publish：

```powershell
dotnet publish -c Release -p:Platform=x64 --no-self-contained -o .\bin\publish
```

Self-contained publish：

```powershell
dotnet publish -c Release -p:Platform=x64 --self-contained -r win-x64 -o .\bin\publish
```

## 4. 运行时文件

WinSentryAI 会将运行时文件放在可执行文件旁：

| 数据 | 位置 |
|---|---|
| 设置 | `settings.ini` |
| 数据库 | `WinSentryAI.db` |
| 日志 | `logs\app-YYYYMMDD.log` |
| API keys | SQLite `AppSettings`，通过 Windows DPAPI 加密 |

API key 绑定当前 Windows 用户账号加密。如果将可携文件夹移动到另一个 Windows 账号，需要重新输入 API key。

## 5. 第一次启动

第一次启动会显示 onboarding 流程，可设置语言与 AI provider。AI 设置可以略过，之后再到 Settings 补设置。

建议使用管理员权限运行。如果未使用管理员权限，部分事件日志，尤其 Security log，可能无法读取。

## 6. API Key 设置

WinSentryAI 采用 Bring Your Own Key 模式。

打开 **Settings > AI Settings**，选择 provider，输入模型名称与 API key 后保存。

云端 API key 会存放于本地，并以 Windows DPAPI 加密，不会以明文写入设置文件。

Provider API key 入口：

- Gemini: https://ai.google.dev/gemini-api/docs/api-key?hl=en
- OpenAI: https://platform.openai.com/api-keys
- Claude: https://console.anthropic.com/

## 7. AI Provider 免费 / 付费限制

免费额度、试用金、可用模型与 rate limit 可能改变。请以 provider console 或 billing page 的实际状态为准。

### Gemini

Gemini API 文档列出 Free Tier 与 Paid Tier，是低成本测试 WinSentryAI 的实用选择。

- Pricing / Free Tier: https://ai.google.dev/gemini-api/docs/pricing?hl=en
- Billing: https://ai.google.dev/gemini-api/docs/billing?hl=en
- API key: https://ai.google.dev/gemini-api/docs/api-key?hl=en
- Google AI Studio: https://aistudio.google.com/

### OpenAI

OpenAI API 主要采用 usage-based billing。是否有试用金或免费用量，取决于当前账号与 billing 状态。

- Pricing: https://platform.openai.com/docs/pricing/
- API docs: https://platform.openai.com/docs/
- API keys: https://platform.openai.com/api-keys
- Billing: https://platform.openai.com/settings/organization/billing/overview
- Prepaid billing help: https://help.openai.com/en/articles/8264778-what-is-prepaid-billing

### Claude

Claude API key 与 usage credits 由 Anthropic Console 管理。实际可用额度需以 Console 或 Billing 为准。

- Pricing: https://docs.anthropic.com/en/docs/about-claude/pricing
- Get started: https://docs.anthropic.com/en/docs/get-started
- API overview: https://docs.anthropic.com/en/api/getting-started
- Console: https://console.anthropic.com/
- Billing / usage credits help: https://support.anthropic.com/en/articles/8977456-how-do-i-pay-for-my-api-usage

### Ollama

Ollama 在本地运行，不需要云端 API key，但需要自行安装 Ollama service 与本地模型。

限制：

- 模型质量与速度取决于本机硬件。
- 大型模型可能需要较多 RAM/VRAM。
- WinSentryAI 只能使用设置的 Ollama endpoint 上可用的模型。
- 本地模式可避免云端 API 用量，但不保证诊断质量更好。

## 8. 隐私与云端 AI

使用云端 AI provider 时，WinSentryAI 可在送出 prompt 前遮蔽高风险个人信息：

- Windows 用户名
- `C:\Users\<name>` 这类用户目录片段
- Email 地址

遮蔽仍会保留诊断所需信息，例如 Event ID、provider name、timestamp、计算机名、IP、域 / 工作组与硬件信息。

Ollama 视为本地 provider，不套用云端 redaction。

## 9. 远程连接设置

远程连接用于回溯查询 Windows 事件日志，不是实时远程监控。

远程连接需要先在远程 Windows 计算机上完成准备。

### 安全注意事项

设置脚本会启用 RPC/DCOM、Remote Event Log Management、WMI 防火墙规则、TCP 135，以及 Remote Registry 服务。请只在受信任网络与您有管理权限的计算机上使用。

默认只套用 Domain 与 Private profile。除非环境明确需要且已有其他网络保护，否则不建议启用 Public profile。

### 远程账号需求

用于连接远程计算机的账号，必须加入远程计算机上的本地组：

- Administrators
- Event Log Readers

如果使用域账号，请将该域账号，或包含该账号的域组，加入远程计算机上的这两个本地组。

### 脚本位置

项目提供：

```text
Scripts\EnableWindowsEventLogViewerPolicy.ps1
```

请将此文件复制到远程计算机，并以管理员权限打开 PowerShell 执行。

### 执行脚本

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\EnableWindowsEventLogViewerPolicy.ps1
```

若 Windows 因文件来自其他计算机而阻止：

```powershell
Unblock-File .\EnableWindowsEventLogViewerPolicy.ps1
```

只针对当前 PowerShell 窗口临时放宽执行策略：

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\EnableWindowsEventLogViewerPolicy.ps1
```

只有环境需要时才启用 Public profile：

```powershell
.\EnableWindowsEventLogViewerPolicy.ps1 -IncludePublicProfile
```

### 远程连接失败时

请检查：

- 远程账号是否已加入远程计算机上的 Administrators 与 Event Log Readers。
- 主机名或 IP 是否可正确解析。
- Windows 防火墙或第三方防火墙是否允许 RPC/Event Log 流量。
- WinSentryAI 所在计算机是否能连接到远程计算机。
- 域、工作组、账号、密码是否正确。
- Security log 可能需要管理员权限。

## 10. 系统托盘行为

关闭主窗口时，WinSentryAI 可以留在系统托盘并持续进行本地监控。第一次出现的 tray hint 会说明这个行为。

常见行为：

- 双击 tray icon 可还原主窗口。
- 使用 tray menu 可显示或退出应用程序。
- 重复启动 WinSentryAI 会唤回已有 instance，而不是启动第二份。

## 11. 主题设置

Settings 提供：

- `Light`
- `Dark`
- `System`

保存设置后会立即套用主题。部分输入框与下拉菜单会刻意使用白底，以确保 Light / Dark 模式都能清楚阅读。

已知主题限制：

- Settings 下拉菜单目前以可读性优先，使用白底，而不是完整深色原生样式。

## 12. Known Issues / Limitations

- Remote connection 是回溯查询，不是实时远程监控。
- Security log 可能需要管理员权限。
- Cloud AI 会收到已遮蔽后的事件摘要。
- API 使用可能产生 provider 费用。
- Ollama 本地模式取决于已安装模型与本机硬件性能。
- Theme 下拉菜单目前使用白底以确保可读性。
- AI 输出可能不完整或不正确，必须由 IT 人员判断。
- v0.9 为 release candidate，正式稳定版打包仍在进行中。

## 13. 相关文件

以下独立文件会先保留作为快速参考：

- [AI Provider 免费 / 试用 API 资源](free-api-resource.zh-CN.md)
- [远程连接设置](remote-connection-setup.zh-CN.md)
