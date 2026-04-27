# WinSentryAI 開發日誌 — 2026-04-27

**作業性質**：Stage 6 VM 驗證標記完成 + System Tray 實作 + Stage 7 遠端 Server Event Log 模式
**版本基準**：延續 2026-04-26

---

## 今日完成項目

### 1. Stage 6 VM 驗證完成 ✅

確認前次 VM 執行結果無誤，將 Stage 6（多 Provider 支援：Gemini / OpenAI / Claude / Ollama）標記為完成。

---

### 2. System Tray 實作（Stage 3 Optimization）✅

#### 新增檔案

- **`Services/ITrayService.cs`**：System Tray 服務介面定義（Initialize / SetAlert / ShowBalloon / Dispose）
- **`Services/TrayService.cs`**：`Hardcodet.NotifyIcon.Wpf` 實作

#### 核心技術決策

**程式化圓形圖示生成**：不使用 `.ico` 資源檔，改以 `System.Drawing.Bitmap` + `Graphics.FillEllipse` 動態產生：
- 正常狀態：藍色（`#0063B1`）圓形帶白色 "W" 字
- 警示狀態：紅色（`#C42B1C`）圓形帶白色 "W" 字
- 使用 `GetHicon()` + P/Invoke `DestroyIcon()` 避免 GDI handle 洩漏
- `FontStyle.Bold` 需明確寫 `System.Drawing.FontStyle.Bold`（解決 WPF `FontStyle` 與 GDI 型別歧義）

**縮小至系統匣而非關閉**：
- `App.xaml.cs` 加入 `public bool IsShuttingDown { get; set; }` 旗標
- `ShutdownMode` 改為 `OnExplicitShutdown`（隱藏主視窗不觸發 App 結束）
- `MainWindow.OnClosing` 檢查旗標：真正關閉 → 允許；否則 `e.Cancel = true; Hide()`
- Tray 右鍵「結束」：設定 `IsShuttingDown = true` → `Application.Current.Shutdown()`

**Critical/Error 事件通知**：
- `MainViewModel` 在 `StartWatching` 回調中，若事件等級 ≤ Error 則呼叫 `Tray.SetAlert(true)` + `Tray.ShowBalloon()`

#### i18n 字串新增（Tray 相關）

新增以下 key 至三份語言檔（en / zh-TW / zh-CN）：
`Tray_Menu_Show`、`Tray_Menu_Exit`、`Tray_Tooltip_Normal`、`Tray_Tooltip_Alert`、`Tray_Balloon_Title`、`Tray_Balloon_Message`

#### Build 驗證

`dotnet build -c Debug -p:Platform=x64` → **0 errors, 0 warnings** ✅

---

### 3. Stage 7：遠端 Server Event Log 模式 ✅

#### 新增檔案

| 檔案 | 說明 |
|------|------|
| `Services/RemoteEventLogService.cs` | 實作 `IEventLogService`，以 `EventLogSession` API 連線遠端主機 |
| `ViewModels/RemoteConnectionViewModel.cs` | 連線 Dialog 的 ViewModel |
| `Views/RemoteConnectionWindow.xaml` | 遠端連線 Dialog UI |
| `Views/RemoteConnectionWindow.xaml.cs` | Dialog code-behind（讀取 PasswordBox 並回傳結果） |

#### RemoteEventLogService 設計重點

- `EventLogSession` 支援兩種模式：
  - 使用目前 Windows 憑證：`new EventLogSession(hostname)`
  - 指定帳號：`new EventLogSession(hostname, domain, username, securePass, SessionAuthentication.Default)`
- `StartWatching` / `StopWatching` 為 no-op（遠端不支援即時訂閱）
- 加入 `public string Hostname => _hostname` 屬性供 UI 顯示
- **命名空間歧義修正**：`System.Diagnostics.Eventing.Reader` 中的 `EventRecord` 和 `EventLevel` 與 `WinSentryAI.Models` 中的同名型別衝突，以 `using alias` 解決：
  ```csharp
  using AppEventRecord = WinSentryAI.Models.EventRecord;
  using AppEventLevel = WinSentryAI.Models.EventLevel;
  using WinApiEventRecord = System.Diagnostics.Eventing.Reader.EventRecord;
  ```

#### RemoteConnectionViewModel 設計重點

- 使用 `CredentialMode = "Current" | "Specify"` 字串屬性（非 bool），搭配現有 `StringEqualityConverter` 做 RadioButton binding，不需要新增 InverseBoolConverter
- `Password` 屬性由 code-behind 在送出前注入（PasswordBox 安全性限制）
- `ConnectAsync()` 執行連線測試（查詢最近 1 分鐘 1 筆事件），成功後設定 `CreatedService`、儲存 LastHost/LastDomain/LastUsername 至 `settings.ini [Remote]`
- 密碼不儲存，僅在本次連線期間保留於 `SecureString`

#### MainViewModel 遠端模式整合

新增屬性與命令：
- `IsRemoteMode`、`RemoteHost`（顯示於橫幅）
- `ConnectRemoteCommand`（非同步，透過 delegate 開啟 Dialog）
- `DisconnectRemoteCommand`（中斷連線、釋放資源、回到本機模式重新初始化）
- `ShowConnectDialogAsync`：`Func<Task<(RemoteEventLogService?, int)>>?`，由 MainWindow code-behind 注入，保持 ViewModel 不依賴 View 型別

#### MainWindow.xaml UI 變更

1. **遠端模式橫幅**：在主內容區上方，`IsRemoteMode = True` 時顯示藍色橫幅，內含主機名稱 + 「中斷連線」按鈕
2. **Status Bar「連線至遠端」按鈕**：常駐顯示於右下角，點擊開啟連線 Dialog

#### Settings 頁新增 Remote 區塊

- `SettingsView.xaml` 新增 Remote Connection 卡片，顯示 LastHost / LastDomain / LastUsername（唯讀）
- `SettingsViewModel.cs` 新增三個 observable 屬性，於 `LoadSettings()` 讀取 `[Remote]` 區段

#### i18n 字串新增（Remote 相關）

新增以下 key 至三份語言檔（en / zh-TW / zh-CN）：
`Remote_Dialog_Title`、`Remote_Label_Hostname`、`Remote_Placeholder_Hostname`、`Remote_Label_Credentials`、`Remote_Radio_CurrentUser`、`Remote_Radio_SpecifyUser`、`Remote_Label_Domain`、`Remote_Placeholder_Domain`、`Remote_Label_Username`、`Remote_Label_Password`、`Remote_Note_PasswordNotSaved`、`Remote_Label_QueryRange`、`Remote_Range_1h/6h/24h/7d`、`Remote_Button_Connect`、`Remote_Button_Cancel`、`Remote_Button_Disconnect`、`Remote_Status_Connecting`、`Remote_Status_Success`、`Remote_Status_Failed`、`Remote_Banner_ActiveMode`、`Remote_Status_Loaded`、`Settings_Section_Remote`、`Settings_Label_LastHost/LastDomain/LastUsername`

#### Build 驗證

`dotnet build -c Debug -p:Platform=x64` → **0 errors, 0 warnings** ✅

---

## 待辦（下一階段）

- Stage 8：去識別化（Privacy 階段）— 對雲端 AI 傳送前過濾高風險個資（帳號名稱、路徑中帳號段、Email）
- 非管理員啟動警告 Dialog（`non-admin-mockup.html`）
- Toast 通知堆疊邏輯（最多 3 則）
- About 對話框（版本號 + 依賴套件 + MIT 授權）
- 設定頁「清除所有日誌」按鈕

---

## 晚間追記（Codex 作業 + UX / 視覺待討論項目）

### 已完成 / 已驗證

- Stage 8 Privacy Redaction 第一版完成：
  - 雲端 AI（Gemini / OpenAI / Claude）送出前遮蔽高風險個資：使用者名稱、`C:\Users\<name>` 路徑片段、Email。
  - Ollama 保持本機原始資料，不套用 redaction。
  - `AnalysisResults.Prompt` 儲存 redacted user message。
  - AI 分析面板顯示本機 redaction map。
- Follow-up Chat 顯示邏輯修正：
  - 初始事件資料 prompt 仍保留在內部 chat history，供後續追問帶上下文。
  - UI 不再把初始 prompt 顯示成使用者藍色訊息泡泡。
  - 改顯示摘要提示：「已提供事件資料、系統環境與上下文日誌給 AI 作為本次分析依據。」
- 非管理員模式提示第一版完成：
  - 啟動時提示非管理員限制。
  - 主視窗顯示受限模式 banner。
- System Tray 第一版完成：
  - 關閉主視窗改為隱藏到系統匣。
  - Tray 右鍵選單提供顯示 / 結束。
  - Critical / Error 事件會切換警示狀態並顯示 balloon。
- Settings 維護功能完成：
  - 清除所有日誌：刪除 `Events`、`ContextLogs`、`AnalysisResults`，保留 API keys、設定與系統快照。
  - About 對話框。
- Build 輸出規範整理：
  - 固定使用 `dotnet build -c Debug -p:Platform=x64`。
  - 固定輸出至 `bin\build\`。
  - 不產生 Linux、x86、ARM64 build。
  - 已清理 `bin\Debug`、`bin\x64`、`bin\verify-build` 等舊輸出。

### Build 驗證

`dotnet build -c Debug -p:Platform=x64` → **0 errors, 0 warnings** ✅

輸出位置：

```text
bin\build\
```

### 新增待討論 TODO

#### 1. Onboarding 應允許略過 AI 設定

目前 Onboarding Step 3 沒有「略過」按鈕。若使用者沒有 Gemini / OpenAI / Claude API key，也沒有 Ollama，就無法進入主視窗。

產品判斷：
- AI 是加值診斷功能，不應阻擋本機事件檢視主流程。
- 使用者應能先進入主視窗查看事件日誌，再到 Settings 補設定 AI。

待討論實作方向：
- Onboarding Step 3 加入「稍後設定 / Skip for now」。
- Skip 後仍寫入 `[General] HasCompletedOnboarding=true`。
- 主視窗 AI 面板顯示「尚未設定 AI」狀態。
- Settings 頁提供完整補設定流程。

#### 2. App Icon 尚未設計

目前正式 icon 尚未設計，Tray icon 只是程式化產生的藍 / 紅色圓形 "W"。

待討論範圍：
- 視窗 icon。
- exe icon。
- system tray normal / alert icon。
- About dialog / GitHub Release / README 視覺。

候選方向：
- Windows Event Log + shield / pulse。
- Sentinel / radar / diagnostic lens。
- W 字母 + log lines。
- AI node + Windows window frame。
- Minimal blue/red alert dual-state icon。

可由 Codex 先產出數個 SVG 候選，再定案轉 `.ico`。

#### 3. UI 與 HTML mockup 差距仍大

目前主程式功能已可運作，但 Settings 與主 UI 仍偏「WPF 控制項堆疊」，與 `settings-mockup.html` / `main-mockup.html` 的產品感有差距。

觀察到的問題：
- Settings 頁大片空白較多，表單密度與分組層級不夠穩定。
- Card 標題、內容、分隔線層級不夠精緻。
- 左側 navigation 選取狀態與品牌感偏弱。
- 按鈕色彩語意不夠一致。
- 表單欄位寬度、label 對齊、section 間距需整理。

建議方向：
- Codex 依 HTML mockup 進行 WPF 設計系統落地，而不是直接用圖形 AI 生成 UI。
- 圖形 AI 較適合 app icon、logo、README / release banner、空狀態插圖。
- 優先項目：
  1. `UX-P1`：Onboarding 可略過 AI 設定。
  2. `Design-P1`：App icon / tray icon / exe icon 設計。
  3. `UI-P1`：Settings 頁視覺重構，對齊 `settings-mockup.html`。
  4. `UI-P2`：Shell / SideNav / StatusBar 視覺整理。
  5. `UI-P2`：AI 分析面板聊天區與分析結果排版優化。
