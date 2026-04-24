import os

# 定義文件內容
md_content = """# WinSentryAI 專案開發規格書 (Project Specification)

## 1. 專案簡介
* **專案名稱**: WinSentryAI
* **目標**: 透過 AI 技術分析 Windows 本機事件日誌 (Event Viewer)，協助使用者快速排查系統異常原因，並提供建議解決方案。
* **核心價值**: 輕量化、隱私優先、AI 輔助診斷。

## 2. 技術棧 (Tech Stack)
* **開發環境**: Visual Studio 2026 Community, Windows 11
* **程式語言**: C#
* **目標框架**: .NET 8.0 (LTS)
* **使用者介面 (UI)**: WinUI 3 (Windows App SDK)
* **資料庫**: SQLite (使用 `Microsoft.Data.Sqlite`)
* **通訊協定**: Windows Event Log API (WinEvt / `System.Diagnostics.Eventing.Reader`)
* **部署模型**: 單一封裝 (Packaged MSIX)，目標支援 Windows 10 & 11

## 3. 功能規格

### 3.1 事件採集與監控
* **讀取機制**: 使用原生 WinEvt API 進行高效能讀取。
* **篩選邏輯**:
    * 預設僅抓取 **嚴重 (Critical)**、**錯誤 (Error)**、**警告 (Warning)** 等級的事件。
    * 提供 UI 選項讓使用者自訂過濾等級（包含一般資訊 Information）。
* **上下文擷取 (Context Capture)**: 
    * 當偵測到異常事件時，自動抓取該時間點前後 1 分鐘內的所有日誌（包含一般資訊），以提供 AI 充足的因果關係分析。
* **即時監控**: 利用事件訂閱機制，第一時間捕捉系統錯誤。

### 3.2 資料存儲與維護
* **本地資料庫**: 使用 SQLite 存儲結構化日誌資訊與 AI 分析結果。
* **旋轉機制 (Log Rotation)**:
    * 預設保留 7 日內數據。
    * 提供設定選項讓使用者延長或縮短保留期間。

### 3.3 AI 分析架構
* **AI 角色定位**: 初篩助手與診斷建議，非自動執行解決方案。
* **支援模型 (BYOK - Bring Your Own Key)**:
    * **Google Gemini (首選)**: 提供免費 API Key 申請教學。
    * **Ollama (本地端)**: 針對具備 GPU/NPU 的 AI PC 使用者，提供完全隱私的本地分析選項。
    * **OpenAI GPT**: 作為付費選項提供。
* **去識別化 (De-identification)**: (規劃中) 在傳送至雲端 AI 前，過濾敏感路徑與用戶資訊。

## 4. 權限與安全性
* **本機權限**: 必須以 **系統管理員身分 (Administrator)** 執行，以讀取 System 與 Security 日誌。
* **數位簽章**: 開源階段考慮使用 Gittag SHA256 校驗或自簽憑證；分發階段考慮符合 Windows 11 安全規範的方案。

## 5. 開發藍圖 (Roadmap)
* **第一階段 (MVP)**: 實作 C# 讀取 Event Log 並存入 SQLite，完成基礎 UI 列表顯示。
* **第二階段 (AI Core)**: 串接 Gemini API 與 Ollama，實作針對單一事件的 AI 請求與解析。
* **第三階段 (Optimization)**: 實作上下文擷取邏輯、日誌自動旋轉、以及介面優化 (Mica 效果)。
* **第四階段 (Privacy)**: 加入去識別化處理邏輯。

---
*文件建立日期: 2026-04-24*
*版本: v1.0*
"""

# 寫入檔案
file_path = "WinSentryAI_Spec.md"
with open(file_path, "w", encoding="utf-8") as f:
    f.write(md_content)

print(f"文件已生成：{file_path}")