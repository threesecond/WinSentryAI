# WinSentryAI 開發日誌 — 2026-04-26

**作業性質**：Stage 4 驗證 + Stage 5 多輪對話 + System Info View + Onboarding Wizard + Stage 6 多 Provider
**版本基準**：延續 2026-04-25 v1.7

---

## 今日完成項目

### 1. 確立 Gemini CLI 協作模式 ✅

建立新的開發分工：
- **Claude**：全 code review、安排工作進度、提供需求描述提示詞、限縮每步 scope
- **Gemini CLI**：負責實際寫 code，每步完成後 build 確認
- 每步範圍嚴格限制在指定檔案，不允許超範圍修改

### 2. Stage 4 Build 驗證通過 ✅

執行 `dotnet build -c Debug -p:Platform=x64`，0 errors，確認 Stage 4 所有架構程式碼（GeminiAIService、EventDetailViewModel、SettingsView API Key 區塊等）編譯正確。

顯示亂碼原因確認為 Windows 終端機 code page 問題（非 UTF-8 預設），不影響建置結果，無需修正。

### 3. VM 執行驗證：發現 Gemini 模型名稱錯誤 ✅（問題已修正）

在 VM 中執行後，點選 AI Analyze 時出現 404 錯誤：

```
[Not Found] Model or endpoint not found (404).
Detail: NOT_FOUND: models/gemini-1.5-flash is not found for API version v1beta
```

根因：`gemini-1.5-flash` 此名稱在 v1beta API 不存在，正確應使用 `gemini-2.0-flash`。

### 4. 移除 Codex 偽 Provider ✅

發現兩處 Codex（OpenAI Codex）被偷偷加入 AI provider 清單：

- `ViewModels/SettingsViewModel.cs`：AiProviders 清單含 `"codex"`
- `CODEX.md` 第 180 行：Stage 6 provider 順序列出 `` `Claude` 或 `Codex` ``

全部移除，確認 `.cs`、`.xaml`、`.md` 所有檔案已無 codex 作為 provider 的殘留。

合法的 provider 清單確定為：`gemini | openai | claude | ollama`

### 5. Gemini 動態模型清單功能 ✅

**動機**：model name 硬編碼容易失效；使用者應從 API 取得實際可用清單後選擇。

**實作內容：**

- **`Services/GeminiAIService.cs`**：
  - `DefaultModel` 從 `gemini-1.5-flash` 改為 `gemini-2.0-flash`
  - 新增 `FetchModelsAsync()` 方法：呼叫 Gemini List Models API (`v1beta/models`)，過濾支援 `generateContent` 的模型，去除 `models/` 前綴後排序回傳

- **`ViewModels/SettingsViewModel.cs`**：
  - 新增 `GeminiModels`（ObservableCollection）、`IsFetchingGeminiModels`、`FetchGeminiModelsCommand`
  - `FetchGeminiModelsAsync()`：cast `AppState.Instance.AI` 為 `GeminiAIService`，呼叫 `FetchModelsAsync()`，填入 `GeminiModels`，若目前選定模型不在清單中則切換至第一筆
  - 修正 field initializer 及 `SaveSettings()` fallback 均從 `gemini-1.5-flash` 改為 `gemini-2.0-flash`
  - 修正 MVVMTK0034 警告（`IsFetchGeminiModelsRunning` 改為引用 generated property）

- **`Views/SettingsView.xaml`**：
  - Gemini Model 欄位從 TextBox 改為可編輯 ComboBox（`IsEditable="True"`），ItemsSource 綁定 `GeminiModels`
  - 新增「Test & Fetch Models」按鈕，`IsEnabled` 綁定 `IsGeminiKeySet`（需先儲存 Key 才能 fetch）

- **`Resources/Strings/Strings.{en,zh-TW,zh-CN}.xaml`**：
  - 新增 `Settings_Button_FetchGeminiModels`
  - 新增 `Settings_GeminiKey_FetchFailed`

### 6. Stage 4 VM 完整驗證通過 ✅

使用 `gemini-2.0-flash` 與 `gemini-2.5-flash-preview` 兩個模型在 Win10/11 VM 各別驗證：
- Settings 頁 API Key 儲存、Fetch Models、模型選擇、Save 全流程正常
- AI Analyze 呼叫成功，結果顯示、持久化正常
- 失敗狀態可見，不阻塞主流程

### 7. Stage 5：多輪對話（Follow-up Chat）✅

**新增 / 修改：**

- **`Models/ChatMessage.cs`**（新增）：`record ChatMessage(ChatRole Role, string Content)`、`enum ChatRole { User, Assistant }`
- **`Services/IAIService.cs`**：新增 `SendChatAsync` 預設介面方法（default implementation，未來各 provider 各自覆寫）
- **`Services/GeminiAIService.cs`**：實作 `SendChatAsync`，以 `systemInstruction` + `contents` 陣列傳送完整對話歷史；User → role `"user"`，Assistant → role `"model"`；錯誤回傳 `"[Error]..."` 字串，不丟例外
- **`ViewModels/EventDetailViewModel.cs`**：新增 `ChatHistory`（ObservableCollection）、`ChatInput`（NotifyCanExecuteChangedFor）、`IsChatLoading`、`IsEventAnalyzed`、`SendChatCommand`；首輪以原始事件資料作為 User seed、初始 AI 回覆作為 Assistant seed；`ResetAiState()` 清空對話歷史；ReferenceEquals 保護防止切換事件時 race condition
- **`Converters/ChatRoleToAlignmentConverter.cs`**（新增）：`ChatRole.User` → `HorizontalAlignment.Right`，Assistant → Left
- **`App.xaml`**：註冊 `ChatRoleToAlignmentConverter`
- **`MainWindow.xaml`**：在 AI Success 狀態下加入對話氣泡 ItemsControl（含 alignment converter）、IsChatLoading 動畫、輸入列（TextBox + Send Button + Enter keybinding）
- **`Resources/Strings`**：三語新增 `AI_Chat_Placeholder`、`AI_Chat_Send`

VM 驗證通過，使用者可對同一事件進行多輪追問，切換事件時對話歷史自動清空。

### 8. 系統資訊視圖（System Info View）✅

- **`ViewModels/SystemInfoViewModel.cs`**（新增）：從 DB 最新 SystemSnapshot 載入所有欄位；`IpAddresses` 與 `GpuInfo` 為 JSON 字串，解析後格式化顯示（IP 換行分隔；GPU 每張顯示名稱 + 驅動版本 + 記憶體，RAM = 0 時略過；JSON parse 失敗 fallback 原始字串）
- **`Views/SystemInfoView.xaml`**（新增）：三個 hc:Card 分組（作業系統、硬體資訊、網路資訊），TextWrapping 支援長字串
- **`ViewModels/MainViewModel.cs`**：加入 `_systemInfoViewModel` 欄位，Navigate `"SystemInfo"` 接線
- **`MainWindow.xaml`**：加入 `SystemInfoView` DataTemplate

VM 驗證通過，Hyper-V VM 顯示虛擬 GPU 名稱 + 驅動版本，格式正常。

### 9. CLAUDE.md 補充 SQLite 自動建立行為 ✅

`CLAUDE.md` 原本未明確記載 DB 檔案自動建立機制。補充說明：`Microsoft.Data.Sqlite` 在首次 `connection.Open()` 時若 `.db` 不存在會自動建立，搭配 `CREATE TABLE IF NOT EXISTS`，無需額外存在性檢查。同時標注 Portable 部署情境。

### 10. Onboarding Wizard（首次啟動引導）✅

首次啟動（`settings.ini` 不存在或 `[General] HasCompletedOnboarding` 非 `true`）自動顯示 4 步驟精靈。

**新增 / 修改：**

- **`ViewModels/OnboardingViewModel.cs`**（新增）：
  - `CurrentStep` 1–4 控制流程，`StepIndex = CurrentStep - 1` 供 hc:StepBar 使用
  - Step 1 語言選擇，`OnSelectedLanguageChanged` 即時呼叫 `App.ApplyLanguage` 切換 UI 語言
  - Step 2 AI Provider 選擇（gemini / openai / claude / ollama）
  - Step 3 `TestAndSaveAsync`：Ollama 呼叫 `/api/tags` 測試；其他 provider 儲存 Key 後以 `IAIService.IsConfiguredAsync` 驗證
  - Step 4 摘要頁，`Finish` 寫入 `[General] HasCompletedOnboarding=true` 後關閉視窗
  - `CanNext` Step 3 需 `TestPassed = true` 才允許前進

- **`OnboardingWindow.xaml`**（新增）：
  - 標準 WPF Window（移除 `WindowStyle="None"` 等在 VM 造成 crash 的設定）
  - `hc:StepBar`（`StepIndex` 加 `Mode=OneWay`）+ 4 個 hc:Card（Visibility 於 Style Setter 中設為 Collapsed，DataTrigger 覆蓋為 Visible）
  - Step 3 依 `SelectedProvider` 切換 API Key / Ollama Endpoint 輸入欄位
  - Back / Next / Finish 按鈕各自以 DataTrigger 控制可見性
  - 各步驟描述文字、Step 4 摘要均使用 `{DynamicResource}` 支援語言切換

- **`App.xaml.cs`**：
  - `ShutdownMode = OnExplicitShutdown`（啟動初期），防止 Onboarding 視窗關閉時觸發 App shutdown
  - OnboardingWindow 建立與 `ShowDialog` 包 try/catch + `Log.Fatal`
  - `result != true`（使用者關閉視窗）→ `Shutdown()`
  - MainWindow `Show()` 後恢復 `ShutdownMode = OnMainWindowClose`

- **`App.xaml.cs` 另修**：`ApplyLanguage` 從 `private` 改為 `internal`，讓 OnboardingViewModel 可直接呼叫，移除 Reflection

- **`Resources/Strings`**：三語新增 `Onboarding_Title`、`Onboarding_Step{1–4}_Title`、`Onboarding_Button_{Next/Back/Finish/Test}`、`Onboarding_Test_{Success/Failed}`、`Onboarding_Step4_{Complete/ProviderLabel/LanguageLabel/Desc}`

**修復的 Bug（Onboarding 開發過程）：**

| 問題 | 修正 |
|------|------|
| OnboardingViewModel settings key 誤用 `"AIProvider"` | 改為 `"Provider"` 符合 settings.ini 規格 |
| `OnSelectedLanguageChanged` 用 Reflection 呼叫 private `ApplyLanguage` | `ApplyLanguage` 改 `internal`，直接 cast 呼叫 |
| `WindowStyle="None"` + `AllowsTransparency="True"` 在 Hyper-V VM 無法渲染，silent crash | 移除，改回標準 WPF Window |
| `hc:StepBar.StepIndex` 預設 TwoWay binding，打到唯讀 computed property | 加 `Mode=OneWay` |
| `hc:Card` 各 Step 的 DataTrigger 無法覆蓋 local value `Visibility="Collapsed"` | 移至 Style `<Setter>` 中，DataTrigger 才能正確覆蓋 |
| `MainWindow.xaml.cs` 建構子自建 `MainViewModel` 與 App.xaml.cs 外部設定 DataContext 衝突，導致 `LoadEventsCommand` 打到錯誤的 ViewModel | 移除建構子內重複建立，`MainWindow_Loaded` 改由 `DataContext as MainViewModel` 取得後呼叫 |

VM 驗證通過：Language 切換、Provider 選擇、API Key 測試、Finish 全流程正常，主視窗正常開啟且事件列表載入正確。

---

## 目前狀態

### Stage 4（Gemini Provider 端到端）

| 項目 | 狀態 |
|------|------|
| Build | ✅ 0 errors |
| Settings：API Key 儲存/清除 | ✅ VM 驗證 |
| Settings：模型動態 Fetch | ✅ VM 驗證 |
| AI Analyze 呼叫流程 | ✅ VM 驗證（gemini-2.0-flash / gemini-2.5-flash-preview）|
| 分析結果持久化 | ✅ VM 驗證 |
| 失敗狀態顯示 | ✅ VM 驗證 |

### Stage 5（多輪對話）

| 項目 | 狀態 |
|------|------|
| ChatMessage model + IAIService 介面 | ✅ |
| GeminiAIService.SendChatAsync | ✅ |
| EventDetailViewModel chat 邏輯 | ✅ |
| MainWindow.xaml Chat UI | ✅ |
| VM 驗證 | ✅ |

### Onboarding Wizard

| 項目 | 狀態 |
|------|------|
| OnboardingViewModel 4 步驟流程 | ✅ VM 驗證 |
| OnboardingWindow.xaml UI | ✅ VM 驗證 |
| App.xaml.cs 觸發與 ShutdownMode 修正 | ✅ VM 驗證 |

### System Info View

| 項目 | 狀態 |
|------|------|
| SystemInfoViewModel（含 JSON 格式化）| ✅ VM 驗證 |
| SystemInfoView.xaml | ✅ VM 驗證 |
| MainViewModel 導覽接線 | ✅ |

---

### Stage 6（OpenAI / Claude / Ollama Provider）✅（Build 驗證通過）

#### Step A — 新增三個 Service 實作 + App.xaml.cs Provider Switch

| 項目 | 狀態 |
|------|------|
| `Services/OllamaAIService.cs`（新增） | ✅ |
| `Services/OpenAIAIService.cs`（新增） | ✅ |
| `Services/ClaudeAIService.cs`（新增） | ✅ |
| `App.xaml.cs` 4-way provider switch | ✅ |
| `ViewModels/SettingsViewModel.cs` NeedsRestart + RestartCommand | ✅ |
| `Views/SettingsView.xaml` 各 Provider 面板（Style DataTrigger）+ Restart Banner | ✅ |

**Service 設計要點：**

- **OllamaAIService**：`IsConfiguredAsync` = GET `/api/tags` 200 OK；`AnalyzeEventAsync` / `SendChatAsync` = POST `/api/chat` with `stream: false`；Endpoint 從 `settings.ini [AI] OllamaEndpoint` 讀取；不需要 API Key
- **OpenAIAIService**：Bearer token auth；`choices[0].message.content` 解析；SecretKey = `"OpenAiApiKey"`；Model 預設 `gpt-4o`
- **ClaudeAIService**：`x-api-key` + `anthropic-version: 2023-06-01` header；`system` 為 top-level 欄位（非 messages 陣列）；`content[0].text` 解析；`MaxTokens = 4096`；SecretKey = `"ClaudeApiKey"`；Model 預設 `claude-3-7-sonnet-latest`

**Provider 切換需重啟**（by design）：`AppState.Instance.AI` 在 App 啟動時依 `settings.ini` 決定，Settings 頁切換後顯示 Restart Banner，使用者確認後以 `Process.Start(Environment.ProcessPath!)` 重啟。

#### Step B — Settings 頁 OpenAI / Claude Key 管理

| 項目 | 狀態 |
|------|------|
| `SettingsViewModel.cs` OpenAI/Claude 常數、屬性、命令 | ✅ |
| `SettingsViewModel.cs` OpenAi/ClaudeModel 載入/儲存 | ✅ |
| `SettingsViewModel.cs` `RefreshAllKeyStatusAsync`（合併三個 provider 狀態刷新）| ✅ |
| `Views/SettingsView.xaml.cs` 4 個新 Click handler | ✅ |
| `Views/SettingsView.xaml` OpenAI / Claude 面板各自獨立（含 Model TextBox + PasswordBox）| ✅ |
| `Resources/Strings` 三語 OpenAI/Claude 相關字串 | ✅ |
| Build 驗證 | ✅ 0 errors, 0 warnings |

---

## 待執行任務

1. **[待辦] VM 驗證 Stage 6**：在 VM 中測試 OpenAI / Claude / Ollama 各 provider 的 API Key 儲存、分析呼叫、多輪對話全流程
2. **[待辦] UI 重構 — 方案 A（共四步，依序執行，各步獨立 build 驗證）**：見下方詳細規格
3. **[待辦] System Tray**：縮小至系統匣、圖示狀態切換、右鍵選單，使用 `Hardcodet.NotifyIcon.Wpf`

---

## UI 重構規格（方案 A）

### 背景

設計稿（`main-mockup.html`）與目前實作差距主要集中在：版面配置、詳細面板分頁、Toolbar 缺失、等級顯示方式。方案 A 為逐步修補，每步獨立驗證，不做大範圍架構替換。

**執行者：Claude**（全部四步），原因：所有修改均在既有 XAML/ViewModel binding 上操作；Gemini CLI 在本專案的大型 XAML 結構改動有已知可靠性問題（DataTrigger 地雷、false completion）。

---

### Step UI-1 — 事件等級：圓點改文字 Badge

**設計稿對應**：`main-mockup.html` → `.badge-critical / .badge-error / .badge-warning / .badge-info`

**需修改的檔案：**

- `Views/EventListView.xaml`：
  - 將 Level 欄位由 `Ellipse`（紅色圓點）改為 `TextBlock` + `Border`，以 `EventLevelToColorConverter` 控制背景/前景
  - 文字內容：`Critical` / `Error` / `Warning` / `Information`（i18n key 可沿用或新增）
  - 樣式參考：小圓角 Border（CornerRadius="3"），padding 2,8，字重 Bold，字型大小 11

- `Converters/EventLevelToColorConverter.cs`（視現況決定是否拆成兩個 Converter）：
  - 可能需要新增 `EventLevelToBackgroundConverter` 與 `EventLevelToForegroundConverter`，或擴充現有 Converter 以 ConverterParameter 區分模式

- `Resources/Strings/Strings.{en,zh-TW,zh-CN}.xaml`：
  - 若 Level 文字需要 i18n，新增對應 key；若直接用英文固定字串可略過

**完成條件：** Build pass，事件列表 Level 欄顯示彩色文字 Badge，不顯示圓點。

---

### Step UI-2 — 事件表格加入 AI 分析欄位

**設計稿對應**：`main-mockup.html` → 表格第六欄「AI」，顯示「✓ 已分析」或「—」

**需修改的檔案：**

- `Views/EventListView.xaml`：
  - 在 DataGrid/ListView 最右側加入「AI」欄位
  - 若 `EventRecord.IsAnalyzed == true`：顯示「✓ 已分析」（綠色，`SuccessBrush` 或 `#107c10`）
  - 若 false：顯示「—」（`SecondaryTextBrush`）
  - 欄寬約 70px，Header 文字「AI」

- `Models/EventRecord.cs`：確認 `IsAnalyzed` 欄位存在（應已存在，Stage 4 已實作），若無則補上

**完成條件：** Build pass，表格出現 AI 欄，已分析事件顯示綠色勾。

---

### Step UI-3 — 事件列表加入 Toolbar（篩選 + 搜尋）

**設計稿對應**：`main-mockup.html` → `.toolbar` 區塊，含 filter chips + search box + 重新整理按鈕

**需修改的檔案：**

- `Views/EventListView.xaml`：
  - 在事件列表頂部加入 Toolbar（`Border` + `StackPanel`）
  - 左側：篩選 Chips — 全部 / 嚴重 / 錯誤 / 警告（`ToggleButton` 或 `RadioButton` with custom style，圓角 12px）
  - 中間：搜尋框（`hc:SearchBar` 或 `hc:TextBox` with placeholder）
  - 右側：重新整理按鈕（綁定既有 `LoadEventsCommand`）

- `ViewModels/MainViewModel.cs`（EventList 相關部分）：
  - 新增 `FilterLevel` 屬性（`string`，值：`"All" / "Critical" / "Error" / "Warning"`）
  - 新增 `SearchText` 屬性（`string`）
  - 新增 `FilteredEvents`（`ICollectionView` 或 computed `ObservableCollection`），根據 `FilterLevel` 和 `SearchText` 過濾 `Events`
  - `EventListView` 的 `ItemsSource` 改綁 `FilteredEvents`

- `Resources/Strings/Strings.{en,zh-TW,zh-CN}.xaml`：
  - 新增篩選按鈕文字：`EventList_Filter_All / Critical / Error / Warning`
  - 新增搜尋框 placeholder：`EventList_Search_Placeholder`

**完成條件：** Build pass，點選 Chip 過濾事件等級，搜尋框輸入過濾來源/訊息文字，重新整理按鈕運作正常。

---

### Step UI-4 — 詳細面板移至底部 + 三分頁 Tab

**設計稿對應**：`main-mockup.html` → `.detail-panel` 底部面板，含三個 Tab（事件詳細 / AI 分析 / 上下文事件），AI 分析 Tab 底部 pinned 輸入列

**這是工程量最大的一步。**

**需修改的檔案：**

#### `MainWindow.xaml` — 版面重構

目前版面：
```
Grid（2 Columns）
├── Column 0：SideMenu
└── Column 1：Grid（2 Columns）
    ├── Column 0：ContentControl（CurrentViewModel）
    ├── Column 1：GridSplitter
    └── Column 2：右側詳細面板（ScrollViewer）
```

目標版面：
```
Grid（2 Columns）
├── Column 0：SideMenu（+ sidebar resize handle，已在 mockup 設計）
└── Column 1：Grid（2 Rows）
    ├── Row 0：ContentControl（CurrentViewModel）— flex 佔滿
    ├── Row 1：GridSplitter（水平，高度 4px）
    └── Row 2：詳細面板（fixed height，預設 300px）
         ├── Tab Header Bar（事件詳細 / AI 分析 / 上下文事件 + AI Analyze 按鈕）
         └── Tab Content（依選取顯示）
```

移除：右側三欄 Grid、GridSplitter（垂直）、右側 `EventDetailViewModel` DataContext 整個區塊。

新增：水平 `GridSplitter`（`Height="4" HorizontalAlignment="Stretch"`），底部詳細面板。

#### 詳細面板 Tab 結構

- Tab Header：自製 TabBar（`StackPanel` + `RadioButton` / `ToggleButton`，不使用 `TabControl` 避免 HandyControl 樣式衝突），或使用 `hc:TabControl`
- Tab 1「事件詳細」：現有事件欄位 Grid（從右側面板移過來）
- Tab 2「AI 分析」：
  - 可捲動區域（ScrollViewer）：AI 狀態（Idle/NoKey/Loading/Success/Failure）+ AI 結果 + Chat 氣泡
  - **Pinned 底部**（不在 ScrollViewer 內）：Disclaimer 文字列 + Chat 輸入列（TextBox + Send Button）
  - Disclaimer 和輸入列僅在 `IsAiSuccess = true` 時顯示
- Tab 3「上下文事件」：現有 ContextLogs ListView（從右側面板移過來）

#### `ViewModels/EventDetailViewModel.cs`

- 新增 `SelectedTab` 屬性（`string`，值：`"Detail" / "AI" / "Context"`，預設 `"Detail"`）
- 分析成功後自動切換到 `"AI"` Tab：`SelectedTab = "AI";`

#### `Resources/Strings/Strings.{en,zh-TW,zh-CN}.xaml`

新增 Tab 標籤 key：
- `EventDetail_Tab_Detail`：「事件詳細」/ "Event Detail" / etc.
- `EventDetail_Tab_AI`：「AI 分析」/ "AI Analysis" / etc.
- `EventDetail_Tab_Context`：「上下文事件」/ "Context Events" / etc.

**完成條件：** Build pass，底部面板正常顯示，三個 Tab 可切換，AI 分析流程完整，Chat pinned 輸入列固定在面板最底部不隨內容捲動，GridSplitter 可拖拉調整面板高度。

---

### UI 重構注意事項（給下一個 session 的 Claude）

1. **WPF Visibility DataTrigger 地雷**：所有 `Visibility` 控制一律放在 `<Style><Setter>` 中設為 `Collapsed`，用 `<DataTrigger>` 改為 `Visible`。**絕對不能**在元素 attribute 直接寫 `Visibility="Collapsed"` 再用 DataTrigger 覆蓋，local value 優先級高於 Style，DataTrigger 不會生效。

2. **Chat Pinned Input Bar**：AI 分析 Tab 的 ScrollViewer 只包 AI 結果區域；Disclaimer + 輸入列要在 ScrollViewer **外面**，使用 Grid RowDefinitions `Height="*"` + `Height="Auto"` 實現，不能放進 ScrollViewer 裡。

3. **GridSplitter 水平**：`<GridSplitter Grid.Row="1" Height="4" HorizontalAlignment="Stretch" VerticalAlignment="Center" Background="Transparent"/>`，Row 1 設 `Height="4"`。

4. **Tab 切換後 SelectedTab 不得影響現有 EventDetailViewModel 的其他 binding**，只是控制 Tab 內容的 Visibility，不做 ViewModel 替換。

5. **每步完成後執行** `dotnet build -c Debug -p:Platform=x64`，確認 0 errors 再進行下一步。

---

## 架構決策記錄

### Gemini FetchModels 放在 GeminiAIService 而非 IAIService

各 provider 的 model list API 差異很大（OpenAI 有 `/models`，Claude 無公開 list API，Ollama 有 `/api/tags`），強行放進 `IAIService` 會造成不必要抽象。MVP 階段每個 provider 各自在 Settings 中實作，SettingsViewModel 依 provider 類型 cast 後呼叫。

### Onboarding Window 不使用 WindowStyle=None

原設計為無邊框圓角視窗（仿現代 App 風格），但在 Hyper-V VM（Microsoft Basic Display Adapter）上 `AllowsTransparency="True"` 導致視窗無法渲染，silent crash。改回標準 WPF Window，確保在 Server/VM 環境一致運作，符合工具定位。

### MainWindow.xaml.cs 不自建 ViewModel

WPF MVVM 最佳實踐：ViewModel 的建立與生命週期管理由 App（Composition Root）負責，View 只透過 DataContext 取用。避免 MainWindow.xaml.cs 與 App.xaml.cs 各自建立 ViewModel 造成 binding 打到錯誤實例的問題。

---

*日誌建立日期：2026-04-26*
*版本：v2.0*
