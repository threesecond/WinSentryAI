# 遠端連線設定

這份說明用來準備「遠端 Windows 電腦」，讓 WinSentryAI 可以讀取該電腦的 Windows 事件記錄。

請在遠端電腦上執行設定腳本，不是在執行 WinSentryAI 的本機電腦上執行。

## 安全注意事項

此腳本會啟用 RPC/DCOM、遠端事件記錄管理、WMI 防火牆規則、TCP 135，以及 Remote Registry 服務。請只在受信任的網路與你有管理權限的電腦上使用。

預設只會套用到 Domain 與 Private 防火牆設定檔。除非你的環境明確需要，且已有其他網路保護措施，否則不建議套用 Public 設定檔。

## 遠端帳戶權限需求

用來連線到遠端電腦的帳戶，必須在遠端電腦上加入以下本機群組：

- Administrators
- Event Log Readers

如果使用網域帳戶，請將該網域帳戶，或包含該帳戶的網域群組，加入遠端電腦上的這兩個本機群組。若缺少這些權限，WinSentryAI 可能可以連到該電腦，但無法讀取部分或全部事件記錄。

## 腳本位置

專案內提供：

```text
Scripts\EnableWindowsEventLogViewerPolicy.ps1
```

請將這個檔案複製到遠端電腦，並用系統管理員權限開啟 PowerShell 執行。

## 執行方式

1. 使用系統管理員帳號登入遠端電腦。
2. 以系統管理員身分開啟 Windows Terminal 或 PowerShell。
3. 切換到腳本所在資料夾。
4. 執行：

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\EnableWindowsEventLogViewerPolicy.ps1
```

如果檔案是從網路下載，或從其他電腦複製過來，Windows 可能會封鎖此檔案。可先執行：

```powershell
Unblock-File .\EnableWindowsEventLogViewerPolicy.ps1
```

也可以只針對目前 PowerShell 視窗暫時放寬執行原則：

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\EnableWindowsEventLogViewerPolicy.ps1
```

## Public 防火牆設定檔

腳本預設不會啟用 Public 防火牆設定檔。如果你的環境確實需要：

```powershell
.\EnableWindowsEventLogViewerPolicy.ps1 -IncludePublicProfile
```

只有在你理解安全風險時才建議使用。

## 腳本會修改什麼

- 啟用 COM+ Network Access / DCOM inbound 防火牆規則。
- 開放 TCP 135 給 RPC Endpoint Mapper 使用。
- 啟用 Remote Event Log Management 防火牆規則。
- 啟用 Windows Management Instrumentation (WMI) 防火牆規則。
- 將 Remote Registry 服務啟動類型設為 Automatic。
- 啟動 Remote Registry 服務。

## 如果仍然無法連線

請檢查遠端電腦與網路環境：

- 遠端使用者是否已加入遠端電腦上的 Administrators 與 Event Log Readers 群組。
- 主機名稱或 IP 是否能正確解析。
- Windows 防火牆或第三方防火牆是否允許 RPC/Event Log 流量。
- WinSentryAI 所在電腦是否能連到遠端電腦。
- 網域、工作群組、帳號、密碼是否輸入正確。
- 讀取 Security log 可能需要系統管理員權限。
