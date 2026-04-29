# Remote Connection Setup

This guide is for preparing a remote Windows computer so WinSentryAI can read its Windows Event Logs.

Run the setup script on the remote computer, not on the computer running WinSentryAI.

## Security Notice

The script enables RPC/DCOM, Remote Event Log Management, WMI firewall rules, TCP 135, and the Remote Registry service. Use it only on trusted networks and only on computers you administer.

By default, the script applies firewall rules to Domain and Private profiles. Avoid enabling the Public profile unless your environment specifically requires it and has additional network protection.

## Remote Account Requirements

The account used to connect to the remote computer must be a member of these local groups on the remote computer:

- Administrators
- Event Log Readers

If you connect with a domain account, add that domain account or a domain group containing that account to both local groups on the remote computer. Without these permissions, WinSentryAI may connect to the machine but fail to read some or all Event Logs.

## Script Location

The project includes:

```text
Scripts\EnableWindowsEventLogViewerPolicy.ps1
```

Copy this file to the remote computer and run it from an elevated PowerShell session.

## How To Run

1. Sign in to the remote computer with an administrator account.
2. Open Windows Terminal or PowerShell as Administrator.
3. Change to the folder that contains the script.
4. Run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\EnableWindowsEventLogViewerPolicy.ps1
```

If the file was downloaded from the internet or copied from another computer, Windows may block it. Run this first:

```powershell
Unblock-File .\EnableWindowsEventLogViewerPolicy.ps1
```

You can also use a process-only execution policy for the current terminal:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\EnableWindowsEventLogViewerPolicy.ps1
```

## Public Firewall Profile

The script does not enable the Public firewall profile by default. If your environment requires it:

```powershell
.\EnableWindowsEventLogViewerPolicy.ps1 -IncludePublicProfile
```

Use this only when you understand the security impact.

## What The Script Changes

- Enables COM+ Network Access / DCOM inbound firewall rule.
- Allows TCP 135 for RPC Endpoint Mapper.
- Enables Remote Event Log Management firewall rules.
- Enables Windows Management Instrumentation (WMI) firewall rules.
- Sets Remote Registry service startup type to Automatic.
- Starts Remote Registry service.

## If Connection Still Fails

Check these items on the remote computer and network:

- The remote user is a member of both Administrators and Event Log Readers on the remote computer.
- The host name or IP address resolves correctly.
- Windows Firewall or third-party firewall allows RPC/Event Log traffic.
- The computer is reachable from the WinSentryAI computer.
- Domain/workgroup credentials are entered correctly.
- Security log access may require administrator privileges.
