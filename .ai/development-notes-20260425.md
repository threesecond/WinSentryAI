# WinSentryAI 開發日誌 — 2026-04-25

**作業性質**：UI 設計規格（HTML 原型）+ 規格書更新（CLAUDE.md）+ WPF 主程式 Shell / Settings / Event Detail / Context Logs 管線實作與除錯
**版本基準**：延續 2026-04-24 規格書 v1.0

---

## 今日完成項目

### 1. 新增 AI 分析面板錯誤狀態原型（`ai-error-states-mockup.html`）

完成 6 種 AI 分析失敗狀態的 HTML 互動原型，作為 WPF 實作的視覺與行為規格。

### 2. 新增第四個 AI 提供者：Claude (Anthropic)

提供者排序確立為：**Gemini（首選）→ OpenAI → Claude → Ollama**。

### 3. CLAUDE.md 全面整理

修正了 AI Provider 順序、Onboarding 步驟與多輪對話規格的不一致。

### 4. 實作：主視窗 Shell 架構重構 (Phase 1) ✅

已將 `MainWindow` 從單一檔案重構為現代化的 Shell 架構：
- **View 拆分**：建立 `Views/EventListView.xaml`，將事件日誌列表邏輯從 Shell 中解耦。
- **側邊導覽 (SideMenu)**：採用 HandyControl 的 `SideMenu` 實作左側導覽列，預設包含「事件日誌」與「系統資訊」。
- **Master-Detail 佈局**：
  - 採用 7:3 比例分割空間。
  - 右側分析面板在未選取事件時顯示 `hc:Empty` 佔位符。
  - 實作 `SelectedEvent` 繫結，選取事件後自動切換至詳細資訊與 AI 分析面板。
- **資源中心化**：建立 `Resources/Geometries.xaml` 存儲導覽圖示（Monitor, Info, Chart, Settings）的向量路徑，確保 UI 清晰度與一致性。
- **視窗調整**：將初始尺寸調整為 1300x750，以適應多面板佈局。

### 5. 實作：Settings 頁面與 Shell 內容切換 ✅

已完成主視窗內的基本頁面切換，並建立第一版可用的設定頁。

- **新增 ViewModel**：`ViewModels/SettingsViewModel.cs`
- **新增 View**：`Views/SettingsView.xaml`
- **Shell 內容切換**：
  - `MainViewModel` 新增 `CurrentViewModel`
  - 透過 `ContentControl + DataTemplate` 方式切換 `EventListView` / `SettingsView`
  - `SideMenuItem` 直接綁定 `NavigateCommand`
- **設定欄位已接線**：
  - `General.LogRetentionDays`
  - `General.MaxRetroQueryCount`
  - `AI.Provider`
  - `AI.OllamaEndpoint`
  - `AI.OllamaModel`
  - `ErrorHandling.MaxRetryCount`
  - `UI.Theme`
  - `UI.Language`
- **儲存流程修正**：
  - 調整 `SettingsService`，`Set/SetInt/SetBool` 不再自動 `Save()`
  - 改為由 `SettingsViewModel.SaveSettings()` 統一寫入一次
- **語系修補**：補齊設定頁所需的 `en / zh-TW / zh-CN` 資源鍵

### 6. 實作：事件詳細資訊面板與 EventDetailViewModel ✅

右側面板已從純 placeholder 升級為可顯示實際事件內容的詳細檢視區。

- **新增 ViewModel**：`ViewModels/EventDetailViewModel.cs`
- **事件列表選取流程**：
  - `MainViewModel.SelectedEvent`
  - `OnSelectedEventChanged(...)` 轉送到 `EventDetailViewModel.SelectedEvent`
- **右側面板可顯示**：
  - `Source`
  - `ProviderName`
  - `EventId`
  - `Level`
  - `Timestamp`
  - `Host`
  - `IsAnalyzed`
  - `Message`
- **AI 按鈕先保留骨架**：
  - 顯示在 UI 中
  - 暫時停用，並用 tooltip 註明功能尚未啟用

### 7. 實作：ContextLogs 基礎資料流程與補抓機制 ✅

已完成本機模式下 Context Logs 的基本資料流程，並補上 retrospective / watcher 兩條路徑的共用擷取編排。

- **新增介面 / 服務**：
  - `Services/IContextLogCaptureService.cs`
  - `Services/ContextLogCaptureService.cs`
- **EventLogService 擴充**：
  - `GetContextEventsAsync(triggerTimestamp, ct)`
  - 查詢 trigger 前後 ±1 分鐘內所有等級事件（包含 Info）
- **DatabaseService 擴充**：
  - `GetContextLogsAsync(...)`
  - `SaveContextLogsAsync(...)`
  - `HasContextLogsAsync(...)`
  - `DeleteContextLogsAsync(...)`
- **Context capture 策略**：
  - **Retrospective**：事件寫入後背景逐筆立即補抓
  - **Watcher**：改為延後 60 秒後執行完整補抓，避免只抓到前半段 context
  - **Delayed watcher capture** 會先刪除舊資料再重建，防止殘缺結果卡死
- **EventDetailViewModel 補抓流程**：
  - 先讀 DB
  - 若為空則觸發 `EnsureContextLogsAsync(...)`
  - 補抓後再重讀一次
  - 增加 `ReferenceEquals` 保護，避免使用者切換事件時將舊結果塞回新選取項
- **截斷策略已先對齊 AI Prompt 規格**：
  - Critical / Error：15
  - Warning：10
  - Info：5

### 8. 編譯與 XAML / API 相容性除錯 ✅

本日後半段主要花在「把專案拉回可建置、可執行」的整理工作，已清除多個編譯級與 XAML 級問題。

- **修正 MainViewModel**
  - 補回 `StatusText`
  - 將硬編碼狀態字串改為 i18n resource 讀取
- **修正 EventListView.xaml**
  - 移除錯誤的 HandyControl `xmlns`
- **修正 MainWindow.xaml**
  - 移除 `hc:Empty IsEmpty="True"` 這類 HandyControl 3.5.1 不支援的屬性
  - 將錯誤的 `<hc:IconElement .../>` 改為標準 WPF `Path`
- **修正 SettingsViewModel**
  - 移除與 HandyControl 3.5.1 不相容的即時主題切換 API
  - 目前主題切換行為改為：儲存設定後於下次重啟生效
  - 語言切換仍保留即時套用
- **修正 MainWindow.xaml.cs**
  - 補上 `using WinSentryAI.Models;`，解決 `AppState` 找不到的問題

### 9. 建置輸出確認：x64 / ARM64 雙平台均可產出 ✅

確認「只看到 ARM64 執行檔」並不是專案設定錯誤，而是建置時未明確指定平台。

- `.csproj` 的 `<Platforms>x86;x64;ARM64</Platforms>` 設定正確
- 在 ARM64 開發機上若直接跑 `dotnet build`，預設會落到 ARM64
- 明確指定平台後，兩種輸出都正常：
  - `dotnet build -c Debug -p:Platform=x64`
  - `dotnet build -c Debug -p:Platform=ARM64`
- 目前已確認輸出路徑：
  - `bin\x64\Debug\net8.0-windows\WinSentryAI.exe`
  - `bin\ARM64\Debug\net8.0-windows\WinSentryAI.exe`

### 10. 測試環境確認：使用 Hyper-V VM 進行執行驗證 ✅

由於開發機啟用了 Smart App Control，會阻擋本機未簽章執行檔啟動，因此改採 VM 作為實際執行驗證環境。

- 已在 Windows 10 / Windows 11 VM 中確認主視窗可正常開啟
- 確認 SAC 問題與 WPF 啟動流程無關，屬開發機執行政策限制
- 後續建議維持：
  - 本機負責編譯 / 改碼
  - VM 負責實際執行驗證

---

## 目前 UI 設計原型清單

| 檔案 | 狀態 | 說明 |
|------|------|------|
| `main-mockup.html` | ✅ 完成 | 主程式介面：事件列表、AI 分析面板、系統資訊、遠端模式 |
| `settings-mockup.html` | ✅ 完成 | 設定頁：多 Provider 切換、主題、維護 |
| `onboarding-mockup.html` | ✅ 完成 | Onboarding Wizard：4 步驟引導 |
| `remote-dialog-mockup.html` | ✅ 完成 | 遠端連線 Dialog：連線測試含 Server 資訊卡 |
| `ai-error-states-mockup.html` | ✅ 完成 | AI 錯誤狀態：6 種失敗情境實作規格 |

---

## 待執行任務 (MVP 階段)

1. **[待辦] 實機驗證 Context Logs**：在 VM 中確認 retrospective 與 watcher 路徑都能正確產生 context logs，而不只是編譯通過。
2. **[待辦] Gemini Provider 第一步**：建立第一個可用的 AI provider 路徑（API Key、單次事件分析、結果顯示與儲存）。
3. **[待辦] 系統資訊視圖**：建立獨立的 System Info 頁面並接上導覽。
4. **[待辦] Onboarding Wizard**：讓 `ShowOnboarding` 從旗標升級為真正可執行的首次啟動流程。

---

## 架構決策記錄

### 向量圖示資源化 (`Resources/Geometries.xaml`)
為了避免在 XAML 中重複貼上冗長的 Path Data，所有圖示一律存為 `StaticResource`。這不僅讓 `MainWindow.xaml` 保持簡潔，也方便未來統一替換風格。

### Master-Detail 觸發機制
分析面板的顯示邏輯綁定於 `SelectedEvent != null`。當使用者在 `EventListView` 選取項目時，Shell 會透過 DataBinding 自動更新右側內容，無需在 Code-behind 寫額外的事件處理。

### Context Logs 採用獨立 Capture Service
本日新增 `ContextLogCaptureService`，目的不是引入新框架，而是把以下兩條路徑共用的流程集中：

- retrospective 事件補抓
- 使用者點選事件時的 on-demand backfill
- watcher 事件延後完整補抓

這個 service 負責：
- 呼叫 Event Log 查詢
- 過濾 trigger 自身
- 依等級截斷
- 延後 watcher capture
- 視需要刪除並重建舊的 context logs

### Watcher 路徑改為延後 60 秒補抓
一開始的實作曾在 watcher 收到事件當下立即做 context capture，但這會永遠抓不到「後 1 分鐘」事件，導致資料殘缺。

因此本日改為：
- retrospective：立即抓
- watcher：延後 60 秒後再抓完整視窗

這個調整對後續 AI prompt 的品質影響很大，屬本日重要修正。

### 主題切換先退回「下次重啟生效」
HandyControl 3.5.1 的主題 API 與先前採用的呼叫方式不相容，因此本日沒有硬做即時切換 workaround，而是保留：

- 設定值可儲存
- 下次重啟時生效

語言切換仍保留即時套用，避免這一步再引入額外相容性風險。

---

*日誌更新日期：2026-04-25*
*版本：v1.6（補齊 4/25 全日實作與除錯進度）*
