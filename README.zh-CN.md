# WinSentryAI

**WinSentryAI** 是一款可携式 Windows 桌面诊断工具，通过读取 Windows Event Log，并使用 AI 协助 IT 人员判断系统异常原因。

当前版本：**v0.5**  
状态：**开发中，尚未正式 release**

语言：[English](README.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md)

## 功能定位

WinSentryAI 会结合本地事件日志、系统环境快照、事件上下文，以及 AI 辅助分析，协助 Windows 故障排查。

WinSentryAI 适合日常 Windows 故障排查流程：

- 在桌面界面查看重要事件日志
- 启动后可通过系统托盘维持监控
- 需要时调用 AI provider 生成诊断摘要
- 修复决策仍由操作人员自行判断
- 当前聚焦于本地工作站诊断，未来可延伸到远程 Windows Server 日志查看

## 当前功能

- .NET 8 / WPF 桌面 UI
- 本地 Windows Event Log 回溯加载
- 本地 Event Log 实时监控
- 事件筛选与搜索
- 事件详细信息面板
- 触发事件前后文日志采集
- 使用 SQLite 持久化事件、上下文日志、分析结果、设置与系统快照
- 系统信息页
- 针对选中事件执行 AI 分析
- 针对选中事件进行 follow-up chat
- BYOK AI providers：
  - Google Gemini
  - OpenAI
  - Claude
  - Ollama
- 云端 AI provider 的隐私遮蔽
- 本地 redaction substitution map 显示
- 运行时切换语言：
  - English
  - 繁體中文
  - 简体中文
- System tray 集成
- 非管理员受限模式提示
- Settings 页提供 AI provider 设置与维护功能

## AI Providers

WinSentryAI 采用 Bring Your Own Key 模式。

| Provider | 类型 | 说明 |
|---|---|---|
| Gemini | 云端 | 主要 / 默认 provider |
| OpenAI | 云端 | 需要 OpenAI API key |
| Claude | 云端 | 需要 Anthropic API key |
| Ollama | 本地 | 使用本地 Ollama endpoint，不需要 API key |

云端 provider 的 API key 会以 Windows DPAPI 加密后存放于本地。

## 隐私设计

WinSentryAI 采用隐私优先设计。

- 事件数据与设置存放在主程序目录。
- API key 使用 Windows DPAPI 加密。
- 发送到云端 AI 前可遮蔽高风险个人信息：
  - Windows 用户名
  - `C:\Users\<name>` 这类用户目录片段
  - Email 地址
- Ollama 视为本地 provider，不执行 redaction。
- Redaction 会保留诊断所需上下文，例如 Event ID、provider name、timestamp、计算机名、IP、域 / 工作组与硬件信息。
- AI 分析结果仅供参考。WinSentryAI 不会自动执行修复动作。

## Portable 设计

WinSentryAI 以可携式 Windows 工具为设计方向。

运行时产生的数据会放在可执行文件旁边：

| 数据 | 位置 |
|---|---|
| 设置 | `settings.ini` |
| 数据库 | `WinSentryAI.db` |
| 日志 | `logs\app-YYYYMMDD.log` |
| API keys | SQLite `AppSettings`，使用 DPAPI 加密 |

建议放置位置：

- `C:\Tools\WinSentryAI\`
- USB 随身盘或可携工具文件夹

开发或可携式使用时不建议放在 `C:\Program Files\`，避免 Windows VirtualStore 造成写入路径混乱。

## 系统需求

- Windows 10 或更新版本
- 支持 Windows 11
- 支持带 Desktop Experience 的 Windows Server
- Framework-dependent build 需要 .NET 8 Desktop Runtime
- 建议以管理员身份运行，才能完整读取 Event Log
- 最低实用屏幕 / 窗口尺寸：1280 x 720
- 建议屏幕 / 窗口尺寸：1366 x 768 以上；1440 x 900 以上操作更舒适

Server Core 与 Nano Server 不支持，因为 WinSentryAI 是 WPF 桌面应用程序。

## Build

安装 .NET 8 SDK 后执行：

```powershell
dotnet build -c Debug -p:Platform=x64
```

固定 build 输出文件夹：

```text
bin\build\
```

当前开发版聚焦于 **Windows x64**。其他平台目标暂未纳入。

## Publish

Framework-dependent portable build：

```powershell
dotnet publish -c Release -p:Platform=x64 --no-self-contained -o .\bin\publish
```

Self-contained portable build：

```powershell
dotnet publish -c Release -p:Platform=x64 --self-contained -r win-x64 -o .\bin\publish
```

v0.5 当前尚未提供正式 release package。

## 开发状态

v0.5 是可运行的开发里程碑。核心本地诊断与 AI 分析流程已经可用，但 UI polish 与正式 release package 尚未完成。

近期已完成：

- 多 AI provider 流程
- follow-up chat
- 系统信息页
- onboarding wizard
- privacy redaction
- system tray 行为
- 维护功能
- 固定 build 输出路径

已知待办：

- Onboarding 允许跳过 AI 设置
- 设计正式 app / tray icon
- 改善 Settings UI 视觉质量
- 整理 shell、side navigation、status bar 视觉
- 优化 AI 分析面板排版
- 若后续启用远程模式，需进一步整理 remote Event Log 流程
- 准备正式 release package

## 安全注意事项

- Repository 已设置为排除 `settings.ini`、`.db`、logs、本地 AI workspace 与个人工具设置。
- API key 存放于本地，并绑定当前 Windows 用户账号加密。
- 将 portable folder 移动到另一个 Windows 用户账号时，需要重新输入 API key。
- 执行任何 AI 建议前，应由 IT 人员自行判断风险。

## 授权

本项目预计采用 MIT License。
