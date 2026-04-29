# WinSentryAI v0.5.0

Functional development milestone. This is not an official stable release yet.

## Highlights

- WPF desktop UI on .NET 8 with HandyControl
- Local Windows Event Log retrospective loading
- Live local Event Log monitoring
- Event filtering, search, and detail panel
- Context log capture around trigger events
- SQLite persistence for events, context logs, analysis results, settings, and system snapshots
- System information view
- AI analysis for a selected event
- Follow-up chat for the selected event
- BYOK AI providers:
  - Google Gemini
  - OpenAI
  - Claude
  - Ollama
- Cloud AI privacy redaction with local substitution map display
- Runtime language switching:
  - English
  - Traditional Chinese
  - Simplified Chinese
- Onboarding Wizard with AI setup skip
- System tray integration with normal / alert icons
- App, window, taskbar, and tray icon resources
- Remote Event Log retrospective query
- Single-instance behavior: launching again brings the existing window forward
- Non-admin limited-mode warning
- Settings page with AI provider configuration and maintenance actions

## Known Limitations

- Remote Event Log mode still needs broader testing in real office/network environments.
- OpenAI, Claude, and Ollama end-to-end validation depends on available API keys and local models.
- Remote connection error messages are still mostly raw system/API errors.
- UI polish and official release packaging are still in progress.
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
WinSentryAI-v0.5.0-win-x64-portable.zip
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
