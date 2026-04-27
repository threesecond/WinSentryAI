# WinSentryAI

**WinSentryAI** 是一款可攜式 Windows 桌面診斷工具，透過讀取 Windows Event Log，並使用 AI 協助 IT 人員判斷系統異常原因。

目前版本：**v0.5**  
狀態：**開發中，尚未正式 release**

語言：[English](README.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md)

## 功能定位

WinSentryAI 會結合本機事件日誌、系統環境快照、事件前後文，以及 AI 輔助分析，協助 Windows 疑難排解。

WinSentryAI 適合日常 Windows 疑難排解流程：

- 在桌面介面檢視重要事件日誌
- 啟動後可透過系統匣維持監控
- 需要時呼叫 AI provider 產生診斷摘要
- 修復決策仍由操作人員自行判斷
- 目前聚焦於本機工作站診斷，未來可延伸至遠端 Windows Server 日誌檢視

## 目前功能

- .NET 8 / WPF 桌面 UI
- 本機 Windows Event Log 回溯載入
- 本機 Event Log 即時監控
- 事件篩選與搜尋
- 事件詳細資訊面板
- 觸發事件前後文日誌擷取
- 使用 SQLite 持久化事件、上下文日誌、分析結果、設定與系統快照
- 系統資訊頁
- 針對選取事件執行 AI 分析
- 針對選取事件進行 follow-up chat
- BYOK AI providers：
  - Google Gemini
  - OpenAI
  - Claude
  - Ollama
- 雲端 AI provider 的隱私遮蔽
- 本機 redaction substitution map 顯示
- 執行時期切換語言：
  - English
  - 繁體中文
  - 简体中文
- System tray 整合
- 非管理員受限模式提示
- Settings 頁提供 AI provider 設定與維護功能

## AI Providers

WinSentryAI 採用 Bring Your Own Key 模式。

| Provider | 類型 | 說明 |
|---|---|---|
| Gemini | 雲端 | 主要 / 預設 provider |
| OpenAI | 雲端 | 需要 OpenAI API key |
| Claude | 雲端 | 需要 Anthropic API key |
| Ollama | 本機 | 使用本機 Ollama endpoint，不需要 API key |

雲端 provider 的 API key 會以 Windows DPAPI 加密後存放於本機。

## 隱私設計

WinSentryAI 採隱私優先設計。

- 事件資料與設定存放於主程式目錄。
- API key 使用 Windows DPAPI 加密。
- 傳送至雲端 AI 前可遮蔽高風險個資：
  - Windows 使用者名稱
  - `C:\Users\<name>` 這類使用者目錄片段
  - Email 位址
- Ollama 視為本機 provider，不執行 redaction。
- Redaction 會保留診斷所需上下文，例如 Event ID、provider name、timestamp、電腦名稱、IP、網域 / 工作群組與硬體資訊。
- AI 分析結果僅供參考。WinSentryAI 不會自動執行修復動作。

## Portable 設計

WinSentryAI 以可攜式 Windows 工具為設計方向。

執行時產生的資料會放在執行檔旁邊：

| 資料 | 位置 |
|---|---|
| 設定 | `settings.ini` |
| 資料庫 | `WinSentryAI.db` |
| 日誌 | `logs\app-YYYYMMDD.log` |
| API keys | SQLite `AppSettings`，使用 DPAPI 加密 |

建議放置位置：

- `C:\Tools\WinSentryAI\`
- USB 隨身碟或可攜工具資料夾

開發或可攜式使用時不建議放在 `C:\Program Files\`，避免 Windows VirtualStore 造成寫入路徑混亂。

## 系統需求

- Windows 10 或更新版本
- 支援 Windows 11
- 支援具 Desktop Experience 的 Windows Server
- Framework-dependent build 需要 .NET 8 Desktop Runtime
- 建議以系統管理員身分執行，才能完整讀取 Event Log

Server Core 與 Nano Server 不支援，因為 WinSentryAI 是 WPF 桌面應用程式。

## Build

安裝 .NET 8 SDK 後執行：

```powershell
dotnet build -c Debug -p:Platform=x64
```

固定 build 輸出資料夾：

```text
bin\build\
```

目前開發版聚焦於 **Windows x64**。其他平台目標暫未納入。

## Publish

Framework-dependent portable build：

```powershell
dotnet publish -c Release -p:Platform=x64 --no-self-contained -o .\bin\publish
```

Self-contained portable build：

```powershell
dotnet publish -c Release -p:Platform=x64 --self-contained -r win-x64 -o .\bin\publish
```

v0.5 目前尚未提供正式 release package。

## 開發狀態

v0.5 是可運作的開發里程碑。核心本機診斷與 AI 分析流程已可使用，但 UI polish 與正式 release package 尚未完成。

近期已完成：

- 多 AI provider 流程
- follow-up chat
- 系統資訊頁
- onboarding wizard
- privacy redaction
- system tray 行為
- 維護功能
- 固定 build 輸出路徑

已知待辦：

- Onboarding 允許略過 AI 設定
- 設計正式 app / tray icon
- 改善 Settings UI 視覺品質
- 整理 shell、side navigation、status bar 視覺
- 優化 AI 分析面板排版
- 若後續啟用遠端模式，需進一步整理 remote Event Log 流程
- 準備正式 release package

## 安全注意事項

- Repository 已設定排除 `settings.ini`、`.db`、logs、本機 AI workspace 與個人工具設定。
- API key 存放於本機，並綁定目前 Windows 使用者帳號加密。
- 將 portable folder 搬到另一個 Windows 使用者帳號時，需要重新輸入 API key。
- 執行任何 AI 建議前，應由 IT 人員自行判斷風險。

## 授權

本專案預計採用 MIT License。
