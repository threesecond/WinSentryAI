# 远程连接设置

这份说明用于准备“远程 Windows 计算机”，让 WinSentryAI 可以读取该计算机的 Windows 事件日志。

请在远程计算机上执行设置脚本，而不是在运行 WinSentryAI 的本机上执行。

## 安全注意事项

此脚本会启用 RPC/DCOM、远程事件日志管理、WMI 防火墙规则、TCP 135，以及 Remote Registry 服务。请只在受信任的网络与您有管理权限的计算机上使用。

默认只会应用到 Domain 与 Private 防火墙配置文件。除非您的环境明确需要，并且已有其他网络保护措施，否则不建议应用 Public 配置文件。

## 远程账号权限需求

用于连接到远程计算机的账号，必须在远程计算机上加入以下本地组：

- Administrators
- Event Log Readers

如果使用域账号，请将该域账号，或包含该账号的域组，加入远程计算机上的这两个本地组。如果缺少这些权限，WinSentryAI 可能可以连接到该计算机，但无法读取部分或全部事件日志。

## 脚本位置

项目内提供：

```text
Scripts\EnableWindowsEventLogViewerPolicy.ps1
```

请将这个文件复制到远程计算机，并用管理员权限打开 PowerShell 执行。

## 执行方式

1. 使用管理员账号登录远程计算机。
2. 以管理员身份打开 Windows Terminal 或 PowerShell。
3. 切换到脚本所在文件夹。
4. 执行：

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\EnableWindowsEventLogViewerPolicy.ps1
```

如果文件是从网络下载，或从其他计算机复制过来，Windows 可能会阻止此文件。可先执行：

```powershell
Unblock-File .\EnableWindowsEventLogViewerPolicy.ps1
```

也可以只针对当前 PowerShell 窗口临时放宽执行策略：

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\EnableWindowsEventLogViewerPolicy.ps1
```

## Public 防火墙配置文件

脚本默认不会启用 Public 防火墙配置文件。如果您的环境确实需要：

```powershell
.\EnableWindowsEventLogViewerPolicy.ps1 -IncludePublicProfile
```

只有在您理解安全风险时才建议使用。

## 脚本会修改什么

- 启用 COM+ Network Access / DCOM inbound 防火墙规则。
- 开放 TCP 135 给 RPC Endpoint Mapper 使用。
- 启用 Remote Event Log Management 防火墙规则。
- 启用 Windows Management Instrumentation (WMI) 防火墙规则。
- 将 Remote Registry 服务启动类型设为 Automatic。
- 启动 Remote Registry 服务。

## 如果仍然无法连接

请检查远程计算机与网络环境：

- 远程用户是否已加入远程计算机上的 Administrators 与 Event Log Readers 组。
- 主机名或 IP 是否能正确解析。
- Windows 防火墙或第三方防火墙是否允许 RPC/Event Log 流量。
- WinSentryAI 所在计算机是否能连接到远程计算机。
- 域、工作组、账号、密码是否输入正确。
- 读取 Security log 可能需要管理员权限。
