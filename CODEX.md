# CODEX.md

此檔案提供 Codex 在 `WinSentryAI` 專案中的實際開發順序建議，重點不是重述產品規格，而是根據目前程式碼現況，安排最合理的實作路線。

## 目前程式碼狀態摘要

目前 repository 已經具備：

- 可開啟的 WPF 主視窗與基本 shell
- `App.xaml.cs` 的啟動初始化流程
- `SettingsService`、`DatabaseService`、`EventLogService`、`SystemSnapshotService`
- SQLite schema 建立與事件持久化
- 本機事件日誌回溯查詢與 watcher 即時監控
- 基本 i18n 語系資源檔

目前 repository 尚未完成：

- Onboarding Wizard UI 與流程
- Settings 頁面 UI
- Remote Event Log 模式
- AI service abstraction 與各 provider 實作
- Context log 擷取流程
- Analysis result 寫入與顯示流程
- System tray / toast 行為
- 與 mockup 對齊的多頁 MVVM shell

## 開發原則

以垂直切片方式開發，不要按規格文件章節平行開坑。

每一個階段都應該以「能在 Windows VM 裡實際跑起來、可操作、可驗證」為完成目標，而不是只完成資料模型或只完成 UI 外觀。

## 建議開發順序

### 第一階段：穩定本機 MVP Shell

目標：
把目前單一主視窗原型整理成穩定的本機 MVP 外殼，在加入 AI 或遠端功能前先把基礎互動面做好。

工作項目：

1. 將 `MainWindow` 重構為真正的 shell 版型與導覽區域。
2. 建立正式的 `Views/` 與對應 `ViewModels/`，不要把所有 UI 都放在 `MainWindow`。
3. 建立本機事件列表頁，負責：
   - 事件清單
   - 載入狀態
   - 空白狀態
   - 非管理員模式提示
4. 建立系統資訊頁或面板，接上 `SystemSnapshotService`。
5. 將 `MainViewModel` 內硬編碼的中文狀態文字改為資源字串。
6. 整理啟動流程，明確切開視窗初始化與背景服務初始化。

完成定義：

- App 可穩定開啟到 shell
- 事件列表可正常載入
- 系統快照可正常儲存並顯示
- `MainWindow.xaml.cs` 不再承載業務邏輯，只保留 view wiring

### 第二階段：完成 Settings 與首次啟動體驗

目標：
在 AI 串接前，先把設定與首次使用流程做完整。

工作項目：

1. 實作 WPF 設定頁。
2. 提供並持久化以下設定：
   - log retention
   - max retrospective count
   - theme
   - language
   - AI provider selection
   - Ollama endpoint / model 基本欄位
3. 完成首次啟動判斷，不只看 `settings.ini` 是否存在：
   - 若沒有設定檔，顯示 onboarding
   - 若選定雲端 provider 但沒有有效 key，onboarding / settings 要明確提示
4. 建立 4 步驟 onboarding wizard UI。
5. 將 onboarding 完成結果接上實際設定儲存與 secret storage。

完成定義：

- 首次啟動流程可用
- 使用者不需要手改 `settings.ini`
- 已實作 UI 可在執行時切換語言

### 第三階段：完成本機事件診斷資料管線

目標：
在接 AI 前，先讓本機事件診斷資料完整可用。

工作項目：

1. 依規格加入 trigger event 的前後 +/- 1 分鐘 context event 擷取。
2. 建立 `ContextLogs` 的持久化服務邊界。
3. 建立事件詳細檢視區或頁面，顯示：
   - trigger event
   - related context events
   - analyzed / not analyzed 狀態
4. 重新檢查目前去重邏輯：
   - 現在唯一性依賴 `EventId + Source + Timestamp`
   - 要確認這對 retrospective + watcher 重疊資料是否足夠穩定
5. 明確實作非管理員模式下 `Security` log 無法讀取的 UI 呈現。

完成定義：

- 使用者選取事件後，可以看到足夠的本機診斷資訊
- 資料結構已經能支撐 AI prompt 建構
- 即使 AI 尚未完成，主流程也已具備人工診斷價值

### 第四階段：先完成單一 AI Provider 的端到端流程

目標：
先讓一個 provider 真正跑通，再擴充到其他 provider。

建議第一個 provider：
`Gemini`

原因：
它已經是目前規格中的預設與首選，也符合目前 `settings` 預設值。

工作項目：

1. 定義 `IAIService` 與 provider 實作邊界。
2. 完成 prompt builder，資料來源包括：
   - 最新 `SystemSnapshot`
   - 選取的 trigger event
   - 已儲存的 context logs
   - 目前語言設定
3. 只實作一個 provider：
   - API key 讀寫
   - connectivity test
   - 單一事件分析呼叫
   - `AnalysisResults` 持久化
4. 建立 AI 分析面板 UI，至少包含：
   - idle
   - loading
   - success
   - failure
5. 實作結構化回應解析，並在格式不完全符合時能安全降級。

完成定義：

- 使用者可選取事件並執行 AI 分析
- 分析結果可顯示且可持久化
- 失敗狀態可見，且不會卡死主流程

### 第五階段：加入 Follow-up Chat 與 AI 錯誤狀態強化

目標：
把單次分析升級成可實際使用的診斷流程。

工作項目：

1. 為每個選取事件加入記憶體內 chat history。
2. 針對第一個 provider 加入 follow-up 對話。
3. 加入 disclaimer row 與聊天輸入列 UI。
4. 實作主要錯誤狀態：
   - 沒有 key
   - key 無效
   - timeout / retries exhausted
   - network failure
   - quota / rate limit
5. 將 retry policy 放在 AI service layer，而不是 view model。

完成定義：

- 使用者可以對單一事件進行短輪次追問
- 錯誤處理清楚、可測試、可維護

### 第六階段：加入其餘 AI Providers

目標：
在第一個 provider 穩定後，再逐一擴充。

建議順序：

1. `Ollama`
2. `OpenAI`
3. `Claude`

實際以產品決策為準，先統一命名與定位，再實作。

工作項目：

1. 將 provider request / response mapping 完整隔離。
2. 重用相同的 prompt builder 與 chat history 模型。
3. 重用相同 UI 狀態與持久化結構。
4. 加入 provider-specific 的設定驗證與連線測試。

完成定義：

- 切換 provider 不需要分叉 UI 流程
- 所有 provider 共用一致的使用體驗

### 第七階段：加入 Remote Event Log 模式

目標：
本機診斷流程穩定後，再做遠端讀取。

工作項目：

1. 建立 remote connection dialog。
2. 持久化 remote host 的非秘密欄位。
3. 用 `EventLogSession` 實作遠端 retrospective query。
4. 明確區分 remote mode 與 local live watching。
5. 在 UI 中清楚標示事件來源，避免本機與遠端資料混淆。

完成定義：

- 使用者可連線遠端主機並抓取歷史事件
- 不會讓使用者誤以為 remote mode 支援即時監控

### 第八階段：System Tray、Toast 與桌面工具化收尾

目標：
在核心工作流穩定後，再補齊桌面工具的操作體驗。

工作項目：

1. 加入 system tray icon lifecycle。
2. 加入 normal / alert tray icon 狀態。
3. 針對 `High` 與 `Critical` 加入 toast 通知。
4. 實作 close-to-tray 行為。
5. 在 settings 中加入維護功能：
   - clear logs
   - about dialog
   - diagnostic status

完成定義：

- App 具備完整 Windows 工具行為
- 關閉主視窗後仍能維持合理監控體驗

### 第九階段：Privacy Redaction

目標：
等 prompt construction 穩定後，再加上去識別化。

工作項目：

1. 實作 substitution context model。
2. 僅對雲端 providers 啟用 redaction。
3. 保留本機端應保留的原始資料。
4. 在 AI UI 中顯示 substitution map。
5. 將 redacted prompt 儲存到 `AnalysisResults.Prompt`。

完成定義：

- 雲端 prompt 會一致地去識別化
- 本機診斷資訊不會因此失真

## 目前不該優先做的事

在第四階段之前，不要優先做以下事情：

- 一次把所有 AI provider 全部開工
- 先做 system tray 再補主流程
- 提前花大量時間在 publish / sign / release automation
- 在本機 AI 流程還沒跑通前先做 remote mode
- 在第一條完整垂直流程還沒成形前做過度抽象化

## 立刻應做的三件事

如果現在接著開發，最合理的下一步是：

1. 把 `MainWindow` 拆成真正的 shell 與事件列表 / 詳細資訊區域。
2. 建立 settings page，並完整接上 `SettingsService`。
3. 實作 onboarding，讓首次啟動流程不再只是 `ShowOnboarding` 旗標。

## 目前進度基準

根據 repository 目前的實作狀態：

- 基礎架構：不錯
- 本機 MVP shell：部分完成
- AI 工作流：程式碼尚未開始
- Remote 工作流：程式碼尚未開始
- 操作體驗收尾：尚未開始

目前整體實作進度大約為：

`30% 到 35%`

在第一個 AI provider 能於真實 WPF UI 中完成端到端分析前，都以這個區間作為目前進度基準。
