# WinSentryAI — 發佈與目錄說明

## 一、Git 追蹤規則（哪些目錄需要 push）

| 路徑 | 說明 | Git 追蹤 |
|------|------|-----------|
| `.ai/` | AI 開發筆記（大量臨時 .md） | ❌ 忽略（僅 publish-guide.md 例外） |
| `.claude/` | Claude Code workspace 暫存 | ❌ 忽略 |
| `.github/` | GitHub Actions CI/CD 設定 | ✅ 追蹤 |
| `.vs/` | Visual Studio 本地暫存 | ❌ 忽略 |
| `bin/` | 編譯與發佈輸出；一般 build 固定只使用 `bin/build/` | ❌ 忽略 |
| `obj/` | 編譯中間產物 | ❌ 忽略 |
| `Converters/` | WPF 型別轉換器 | ✅ 追蹤 |
| `Models/` | 資料模型（EventRecord、ChatMessage…） | ✅ 追蹤 |
| `Properties/` | 專案屬性（AssemblyInfo 等） | ✅ 追蹤 |
| `Resources/` | 圖示、字串資源（Strings.en/zh-TW/zh-CN） | ✅ 追蹤 |
| `Services/` | 服務層（AI、DB、Settings、EventLog…） | ✅ 追蹤 |
| `ViewModels/` | MVVM ViewModel | ✅ 追蹤 |
| `Views/` | WPF View（.xaml） | ✅ 追蹤 |

**根目錄須追蹤的檔案**：
- `*.csproj` / `*.slnx` — 專案定義
- `App.xaml` / `App.xaml.cs`
- `MainWindow.xaml` / `MainWindow.xaml.cs`
- `OnboardingWindow.xaml` / `OnboardingWindow.xaml.cs`
- `app.manifest`
- `.gitignore` / `.gitattributes`

**根目錄不追蹤的檔案**：
- `CLAUDE.md` / `CODEX.md` / `GEMINI.md` — AI agent 指令文件
- `.aider.conf.yml` / `.mcp.json` — 個人工具設定
- `*.html` — 設計用 AI mockup（非原始碼）
- `settings.ini` — 執行期設定（含 Provider 選擇，每台電腦不同）
- `*.db` — SQLite 資料庫（含加密 API Key，不可公開）
- `logs/` — 執行期 Serilog 日誌

---

## 二、Portable 發佈版必要檔案

### Build 輸出規範

所有 code agent 的一般 build 必須使用：

```powershell
dotnet build -c Debug -p:Platform=x64
```

固定輸出位置：

```text
bin/build/
```

限制：

- 只產生 Windows x64 build。
- 不產生 Linux、x86、ARM64 build。
- 不使用臨時 `-o` 輸出路徑；若因檔案鎖定臨時使用，完成後必須刪除。

### Portable 發佈

目標平台：Windows x64，.NET 8 需另行安裝（Framework-Dependent）。

以 `dotnet publish -c Release -p:Platform=x64 --no-self-contained -o .\bin\publish` 產出，
輸出位置：`bin/publish/`

### 必須包含

```
WinSentryAI.exe                      主程式可執行檔
WinSentryAI.dll                      主程式邏輯 DLL
WinSentryAI.deps.json                .NET 依賴清單（runtime 必讀）
WinSentryAI.runtimeconfig.json       .NET runtime 設定

# 第三方依賴 DLL（NuGet 套件）
CommunityToolkit.Mvvm.dll
HandyControl.dll
Hardcodet.NotifyIcon.Wpf.dll
Microsoft.Data.Sqlite.dll
Microsoft.Extensions.DependencyInjection.dll
Microsoft.Extensions.DependencyInjection.Abstractions.dll
Microsoft.Extensions.Logging.dll
Microsoft.Extensions.Logging.Abstractions.dll
Microsoft.Extensions.Options.dll
Microsoft.Extensions.Primitives.dll
SQLitePCLRaw.batteries_v2.dll
SQLitePCLRaw.core.dll
SQLitePCLRaw.provider.e_sqlite3.dll
Serilog.dll
Serilog.Sinks.File.dll
Serilog.Extensions.Logging.dll
System.Management.dll

# SQLite 原生函式庫（x64 必要）
runtimes/win-x64/native/e_sqlite3.dll

# System.Management Windows 原生（需要）
runtimes/win/lib/net8.0/System.Management.dll
```

### 不需要包含（可刪除以縮小發佈包）

```
WinSentryAI.pdb                      Debug 符號（Release 可省略）

# 非 Windows 平台 runtime（全部可刪）
runtimes/linux-*/
runtimes/linux-musl-*/
runtimes/osx-*/
runtimes/maccatalyst-*/
runtimes/browser-wasm/

# 非 x64 Windows runtime（若僅發佈 x64）
runtimes/win-arm/
runtimes/win-arm64/
runtimes/win-x86/
```

### 執行後自動產生（不預先打包）

```
settings.ini          首次啟動由 SettingsService 建立
winsentry.db          首次啟動由 DatabaseService 建立
logs/                 首次寫入 log 時由 Serilog 建立
```

---

## 三、目標平台需求

- OS：Windows 10 21H2 以上（需 WinRT API）
- .NET：.NET 8 Desktop Runtime（x64）
- 權限：需以**系統管理員**身份執行（讀取 Windows Event Log 需要）
- 網路：使用雲端 AI（Gemini / OpenAI / Claude）時需對外連線

---

## 四、未來 Self-Contained 發佈（備用）

若要讓使用者免安裝 .NET Runtime：

```
dotnet publish -c Release -p:Platform=x64 --self-contained -r win-x64
```

發佈包會增加約 150–200 MB（含完整 .NET runtime），
但使用者無需預先安裝 .NET 8。
