# Security Policy

## Project Scope

WinSentryAI is a diagnostic assistant for Windows Event Logs. It reads local or remote Windows Event Logs, captures related context events, and can request AI-assisted diagnostic summaries.

WinSentryAI is not a vulnerability scanner, exploit tool, malware analysis tool, password recovery tool, or automatic remediation system. It does not automatically repair systems, modify Windows Event Logs, or execute remediation commands based on AI output.

## Supported Versions

| Version | Status |
|---|---|
| v0.9.x | Release candidate, active stabilization |
| older versions | Best effort only |

## Reporting Security Issues

Please do not disclose security issues publicly before maintainers have had a reasonable opportunity to investigate.

Report security issues through the GitHub repository issue tracker. If a private reporting channel is added later, this policy will be updated.

Please include:

- A clear description of the issue.
- Steps to reproduce.
- Affected version or commit.
- Expected and actual behavior.
- Any relevant logs or screenshots, with secrets and personal data removed.

## Secrets And Sensitive Data

Do not include API keys, passwords, private hostnames, private logs, or unredacted personal data in public issues.

Cloud AI API keys are stored locally using Windows DPAPI. Remote passwords are not saved.

## Remote Setup Script

`Scripts\EnableWindowsEventLogViewerPolicy.ps1` changes the remote computer's Windows configuration by enabling RPC/DCOM, Remote Event Log Management, WMI firewall rules, TCP 135, and the Remote Registry service. Run it only on trusted networks and only on computers you administer.

## No Security Warranty

WinSentryAI is provided under the MIT License without warranty. AI-generated diagnostic suggestions are advisory and must be reviewed by qualified IT personnel before action is taken.
