# WinSentryAI 使用手冊

版本：v0.5 開發里程碑

## 1. WinSentryAI 是什麼

WinSentryAI 是一款可攜式 Windows 桌面工具，用來讀取 Windows 事件記錄，並請 AI 協助產生診斷摘要。它適合已具備基本 Windows 管理知識的 IT 操作人員。

WinSentryAI 不會自動修復系統。AI 輸出僅供參考，採取任何操作前都應由人員自行判斷。

## 2. 安裝需求

- Windows 10 或更新版本
- 支援 Windows 11
- 支援具 Desktop Experience 的 Windows Server
- Windows x64
- Framework-dependent build 需要 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-US/download/dotnet/8.0)
- 建議使用系統管理員權限，以完整讀取事件記錄
- 最低實用螢幕 / 視窗尺寸：1280 x 720
- 建議螢幕 / 視窗尺寸：1366 x 768 以上

Server Core 與 Nano Server 不支援，因為 WinSentryAI 是 WPF 桌面應用程式。

建議可攜式放置位置：

- `C:\Tools\WinSentryAI\`
- USB 隨身碟或可攜工具資料夾

不建議放在 `C:\Program Files\`，避免 Windows 寫入虛擬化造成設定與資料庫路徑混亂。

## 3. Build 與執行

請先安裝 [.NET 8 SDK](https://dotnet.microsoft.com/en-US/download/dotnet/8.0)，再執行：

```powershell
dotnet build -c Debug -p:Platform=x64
```

固定 build 輸出資料夾：

```text
bin\build\
```

從 build 輸出執行：

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

## 4. 執行時檔案

WinSentryAI 會將執行時檔案放在執行檔旁：

| 資料 | 位置 |
|---|---|
| 設定 | `settings.ini` |
| 資料庫 | `WinSentryAI.db` |
| 日誌 | `logs\app-YYYYMMDD.log` |
| API keys | SQLite `AppSettings`，透過 Windows DPAPI 加密 |

API key 綁定目前 Windows 使用者帳號加密。若將可攜資料夾移到另一個 Windows 帳號，需要重新輸入 API key。

## 5. 第一次啟動

第一次啟動會顯示 onboarding 流程，可設定語言與 AI provider。AI 設定可以略過，之後再到 Settings 補設定。

建議使用系統管理員權限執行。若未使用系統管理員權限，部分事件記錄，尤其 Security log，可能無法讀取。

## 6. API Key 設定

WinSentryAI 採 Bring Your Own Key 模式。

開啟 **Settings > AI Settings**，選擇 provider，輸入模型名稱與 API key 後儲存。

雲端 API key 會存放於本機，並以 Windows DPAPI 加密，不會以明文寫入設定檔。

Provider API key 入口：

- Gemini: https://ai.google.dev/gemini-api/docs/api-key?hl=en
- OpenAI: https://platform.openai.com/api-keys
- Claude: https://console.anthropic.com/

## 7. AI Provider 免費 / 付費限制

免費額度、試用金、可用模型與 rate limit 可能改變。請以 provider console 或 billing page 的實際狀態為準。

### Gemini

Gemini API 文件列出 Free Tier 與 Paid Tier，是低成本測試 WinSentryAI 的實用選擇。

- Pricing / Free Tier: https://ai.google.dev/gemini-api/docs/pricing?hl=en
- Billing: https://ai.google.dev/gemini-api/docs/billing?hl=en
- API key: https://ai.google.dev/gemini-api/docs/api-key?hl=en
- Google AI Studio: https://aistudio.google.com/

### OpenAI

OpenAI API 主要採用 usage-based billing。是否有試用金或免費用量，取決於目前帳號與 billing 狀態。

- Pricing: https://platform.openai.com/docs/pricing/
- API docs: https://platform.openai.com/docs/
- API keys: https://platform.openai.com/api-keys
- Billing: https://platform.openai.com/settings/organization/billing/overview
- Prepaid billing help: https://help.openai.com/en/articles/8264778-what-is-prepaid-billing

### Claude

Claude API key 與 usage credits 由 Anthropic Console 管理。實際可用額度需以 Console 或 Billing 為準。

- Pricing: https://docs.anthropic.com/en/docs/about-claude/pricing
- Get started: https://docs.anthropic.com/en/docs/get-started
- API overview: https://docs.anthropic.com/en/api/getting-started
- Console: https://console.anthropic.com/
- Billing / usage credits help: https://support.anthropic.com/en/articles/8977456-how-do-i-pay-for-my-api-usage

### Ollama

Ollama 在本機執行，不需要雲端 API key，但需要自行安裝 Ollama service 與本機模型。

限制：

- 模型品質與速度取決於本機硬體。
- 大型模型可能需要較多 RAM/VRAM。
- WinSentryAI 只能使用設定的 Ollama endpoint 上可用的模型。
- 本機模式可避免雲端 API 用量，但不保證診斷品質較佳。

## 8. 隱私與雲端 AI

使用雲端 AI provider 時，WinSentryAI 可在送出 prompt 前遮蔽高風險個資：

- Windows 使用者名稱
- `C:\Users\<name>` 這類使用者目錄片段
- Email 位址

遮蔽仍會保留診斷所需資訊，例如 Event ID、provider name、timestamp、電腦名稱、IP、網域 / 工作群組與硬體資訊。

Ollama 視為本機 provider，不套用雲端 redaction。

## 9. 遠端連線設定

遠端連線用於回溯查詢 Windows 事件記錄，不是即時遠端監控。

遠端連線需要先在遠端 Windows 電腦上完成準備。

### 安全注意事項

設定腳本會啟用 RPC/DCOM、Remote Event Log Management、WMI 防火牆規則、TCP 135，以及 Remote Registry 服務。請只在受信任網路與你有管理權限的電腦上使用。

預設只套用 Domain 與 Private profile。除非環境明確需要且已有其他網路保護，否則不建議啟用 Public profile。

### 遠端帳戶需求

用來連線遠端電腦的帳戶，必須加入遠端電腦上的本機群組：

- Administrators
- Event Log Readers

如果使用網域帳戶，請將該網域帳戶，或包含該帳戶的網域群組，加入遠端電腦上的這兩個本機群組。

### 腳本位置

專案提供：

```text
Scripts\EnableWindowsEventLogViewerPolicy.ps1
```

請將此檔案複製到遠端電腦，並以系統管理員權限開啟 PowerShell 執行。

### 執行腳本

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\EnableWindowsEventLogViewerPolicy.ps1
```

若 Windows 因檔案來自其他電腦而封鎖：

```powershell
Unblock-File .\EnableWindowsEventLogViewerPolicy.ps1
```

只針對目前 PowerShell 視窗暫時放寬執行原則：

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\EnableWindowsEventLogViewerPolicy.ps1
```

只有環境需要時才啟用 Public profile：

```powershell
.\EnableWindowsEventLogViewerPolicy.ps1 -IncludePublicProfile
```

### 遠端連線失敗時

請檢查：

- 遠端帳戶是否已加入遠端電腦上的 Administrators 與 Event Log Readers。
- 主機名稱或 IP 是否可正確解析。
- Windows 防火牆或第三方防火牆是否允許 RPC/Event Log 流量。
- WinSentryAI 所在電腦是否能連到遠端電腦。
- 網域、工作群組、帳號、密碼是否正確。
- Security log 可能需要系統管理員權限。

## 10. 系統匣行為

關閉主視窗時，WinSentryAI 可以留在系統匣並持續進行本機監控。第一次出現的 tray hint 會說明這個行為。

常見行為：

- 雙擊 tray icon 可還原主視窗。
- 使用 tray menu 可顯示或結束應用程式。
- 重複啟動 WinSentryAI 會叫回既有 instance，而不是啟動第二份。

## 11. 佈景設定

Settings 提供：

- `Light`
- `Dark`
- `System`

儲存設定後會立即套用佈景。部分輸入框與下拉選單會刻意使用白底，以確保 Light / Dark 模式都能清楚閱讀。

已知佈景限制：

- Settings 下拉選單目前以可讀性優先，使用白底，而不是完整深色原生樣式。

## 12. Known Issues / Limitations

- Remote connection 是回溯查詢，不是即時遠端監控。
- Security log 可能需要系統管理員權限。
- Cloud AI 會收到已遮蔽後的事件摘要。
- API 使用可能產生 provider 費用。
- Ollama 本機模式取決於已安裝模型與本機硬體效能。
- Theme 下拉選單目前使用白底以確保可讀性。
- AI 輸出可能不完整或不正確，必須由 IT 人員判斷。
- v0.5 尚未是正式 release package。

## 13. 相關文件

以下獨立文件會先保留作為快速參考：

- [AI Provider 免費 / 試用 API 資源](free-api-resource.zh-TW.md)
- [遠端連線設定](remote-connection-setup.zh-TW.md)
