#requires -RunAsAdministrator

<#
.SYNOPSIS
Enables the Windows firewall and service settings commonly required for remote Windows Event Log access.

.DESCRIPTION
Run this script on the remote computer that WinSentryAI will connect to.
It enables inbound firewall rules for RPC/DCOM, Remote Event Log Management, WMI, and starts the Remote Registry service.

By default, firewall rules are limited to Domain and Private profiles. Use -IncludePublicProfile only when you fully
understand the security impact and the remote computer is protected by other network controls.

.PARAMETER IncludePublicProfile
Also applies the enabled firewall rules to the Public firewall profile.

.NOTES
This script should only be used on trusted networks. Opening RPC/DCOM-related ports on untrusted networks increases
the attack surface of the remote computer.
#>

[CmdletBinding()]
param(
    [switch]$IncludePublicProfile
)

$ErrorActionPreference = "Stop"
$targetProfiles = if ($IncludePublicProfile) { @("Any") } else { @("Domain", "Private") }
$targetProfilesText = $targetProfiles -join ", "

function Enable-RuleGroup {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$DisplayGroups
    )

    foreach ($group in $DisplayGroups) {
        Get-NetFirewallRule -DisplayGroup $group -ErrorAction SilentlyContinue |
            Enable-NetFirewallRule -ErrorAction SilentlyContinue

        Get-NetFirewallRule -DisplayGroup $group -ErrorAction SilentlyContinue |
            Set-NetFirewallRule -Profile $targetProfiles -ErrorAction SilentlyContinue
    }
}

function Enable-RuleByName {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    foreach ($name in $Names) {
        Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue |
            Enable-NetFirewallRule -ErrorAction SilentlyContinue

        Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue |
            Set-NetFirewallRule -Profile $targetProfiles -ErrorAction SilentlyContinue
    }
}

Write-Host "Configuring this computer for remote Windows Event Log access..." -ForegroundColor Cyan
Write-Host "Firewall profiles: $targetProfilesText"

Write-Host "[1/6] Enabling COM+ Network Access / DCOM inbound rule..."
Enable-RuleByName -Names "ComPlusNetworkAccess-DCOM-In"

Write-Host "[2/6] Ensuring TCP 135 inbound is allowed for RPC Endpoint Mapper..."
$rpcRuleName = "WinSentryAI-RPC-Endpoint-Mapper-TCP-135"
$existingRpcRule = Get-NetFirewallRule -Name $rpcRuleName -ErrorAction SilentlyContinue
if (-not $existingRpcRule) {
    New-NetFirewallRule `
        -Name $rpcRuleName `
        -DisplayName "WinSentryAI - RPC Endpoint Mapper (TCP 135)" `
        -Description "Allows remote Windows Event Log access through RPC Endpoint Mapper. Use only on trusted networks." `
        -Direction Inbound `
        -Action Allow `
        -Protocol TCP `
        -LocalPort 135 `
        -Profile $targetProfiles | Out-Null
}
else {
    Enable-NetFirewallRule -Name $rpcRuleName -ErrorAction SilentlyContinue
    Set-NetFirewallRule -Name $rpcRuleName -Profile $targetProfiles -ErrorAction SilentlyContinue
}

Write-Host "[3/6] Enabling Remote Event Log Management firewall rules..."
Enable-RuleByName -Names @(
    "RemoteEventLogSvc-*"
)
Enable-RuleGroup -DisplayGroups @(
    "@FirewallAPI.dll,-29252",
    "Remote Event Log Management*"
)

Write-Host "[4/6] Enabling Windows Management Instrumentation (WMI) firewall rules..."
Enable-RuleByName -Names @(
    "WMI-*"
)
Enable-RuleGroup -DisplayGroups @(
    "Windows Management Instrumentation (WMI)*"
)

Write-Host "[5/6] Starting and enabling Remote Registry service..."
Set-Service -Name RemoteRegistry -StartupType Automatic
Start-Service -Name RemoteRegistry -ErrorAction SilentlyContinue

Write-Host "[6/6] Summary..."
Write-Host "Enabled firewall profiles: $targetProfilesText"
Write-Host "Remote Registry status:"
Get-Service -Name RemoteRegistry | Select-Object Name, Status, StartType | Format-Table -AutoSize

Write-Host "`nDone. Try connecting from WinSentryAI again." -ForegroundColor Green
Write-Host "If the connection still fails, verify credentials, administrator permissions, DNS/name resolution, and network firewall rules."
