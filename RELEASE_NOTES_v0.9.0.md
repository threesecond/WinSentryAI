# WinSentryAI v0.9.0

Release candidate.

## Highlights

- WPF desktop UI on .NET 8 with HandyControl
- Local Windows Event Log retrospective loading
- Live local Event Log monitoring
- Event filtering, search, and detail panel
- Context log capture around trigger events
- SQLite persistence for events, context logs, analysis results, settings, and system snapshots
- System information view
- AI analysis for a selected event with structured section output (Summary, Possible Causes, Recommended Actions, Severity, Additional Information Needed)
- Follow-up chat for the selected event
- BYOK AI providers:
  - Google Gemini (dynamic model list)
  - OpenAI (dynamic model list)
  - Claude (dynamic model list)
  - Ollama (dynamic model list)
- Cloud AI privacy redaction with local substitution map display
- Runtime language switching:
  - English
  - Traditional Chinese
  - Simplified Chinese
- Runtime theme switching with immediate effect:
  - Light
  - Dark
  - System (follows Windows setting)
- Onboarding Wizard with AI setup skip
- System tray integration with normal / alert icons
- First-run tray hint window
- App, window, taskbar, and tray icon resources
- Remote Event Log retrospective query
- Remote connection setup script (`Scripts/EnableWindowsEventLogViewerPolicy.ps1`)
- Single-instance behavior: launching again brings the existing window forward
- Non-admin limited-mode warning
- Settings page with AI provider configuration and maintenance actions
- AI Report view with analysis history and statistics
- Prompt injection defense in AI system prompt
- Improved AI follow-up chat scope (simplified explanations are in scope)

## Changes from v0.5.0

- **Theme switching**: `ThemeService` replaces the placeholder. Light / Dark / System themes now apply immediately on save without restart.
- **AI Report view**: Analysis history with summary statistics, severity distribution, and per-item detail panel.
- **Remote connection error messages**: Friendly error formatting for common RPC, firewall, DNS, and permission failures.
- **Remote connection setup script**: PowerShell script to prepare a remote Windows machine for Event Log queries.
- **AI structured output**: Analysis results parsed into named sections with a Severity badge.
- **Prompt quality**: Follow-up chat scope clarified; simplified-explanation requests are explicitly allowed.
- **Documentation**: Full user manual, API resource guide, and remote connection setup guide added in English, Traditional Chinese, and Simplified Chinese.

## Known Limitations

- Remote Event Log mode still needs broader testing in real office and network environments.
- Claude end-to-end validation depends on available API credits.
- AI suggestions are advisory only. WinSentryAI does not execute fixes automatically.

## Requirements

- Windows 10 or later
- Windows 11 supported
- Windows Server with Desktop Experience supported
- .NET 8 Desktop Runtime x64 for this framework-dependent package
- Administrator privileges recommended for full Event Log access
- Network access required when using cloud AI providers

## Package

Recommended asset:

```text
WinSentryAI-v0.9.0-win-x64-portable.zip
```

Extract the ZIP to a writable portable tools folder such as:

```text
C:\Tools\WinSentryAI\
```

Avoid running from `C:\Program Files\` during portable use because Windows virtualization can redirect writes.

Runtime files are created next to the executable:

- `settings.ini`
- `WinSentryAI.db`
- `logs\app-YYYYMMDD.log`

Cloud provider API keys are stored locally using Windows DPAPI encryption and are tied to the current Windows user account.
