# WinSentryAI

**WinSentryAI** is a portable Windows desktop diagnostic tool that reads Windows Event Logs and uses AI to help IT professionals understand system anomalies.

Current version: **v0.5**  
Status: **active development, not an official release yet**

Languages: [English](README.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md)

## What It Does

WinSentryAI helps with Windows troubleshooting by combining local Event Log data, system environment snapshots, contextual events, and AI-assisted analysis.

WinSentryAI is built for hands-on Windows troubleshooting workflows:

- Review important Event Log entries in a desktop interface
- Keep monitoring available from the system tray after launch
- Ask an AI provider for a diagnostic summary when needed
- Keep remediation decisions in the hands of the operator
- Support local workstation diagnostics today, with room for future remote Windows Server log review

## Current Capabilities

- WPF desktop UI on .NET 8
- Local Windows Event Log retrospective loading
- Live local Event Log monitoring
- Event filtering and search
- Event detail panel
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
- Privacy redaction for cloud AI providers
- Local substitution map display for redacted prompts
- Runtime language switching:
  - English
  - Traditional Chinese
  - Simplified Chinese
- System tray integration
- App, window, taskbar, and tray icon resources
- Single-instance behavior: launching again brings the existing window forward
- Remote Event Log retrospective query
- Non-admin limited-mode warning
- Settings page with AI provider configuration and maintenance actions

## AI Providers

WinSentryAI uses a Bring Your Own Key model.

| Provider | Type | Notes |
|---|---|---|
| Gemini | Cloud | Primary/default provider |
| OpenAI | Cloud | Requires OpenAI API key |
| Claude | Cloud | Requires Anthropic API key |
| Ollama | Local | Uses local Ollama endpoint, no API key required |

Cloud provider API keys are stored locally using Windows DPAPI encryption.

To obtain an API key for each cloud provider:

- **Gemini**: https://ai.google.dev/gemini-api/docs/api-key
- **OpenAI**: https://help.openai.com/en/articles/4936850-where-do-i-find-my-openai-api-key
- **Claude**: https://platform.claude.com/docs/en/api/admin/api_keys/retrieve

## Privacy Model

WinSentryAI is privacy-conscious by design.

- Event data and settings are stored locally in the application directory.
- API keys are encrypted with Windows DPAPI.
- Cloud AI prompts can redact high-risk personal data before sending:
  - Windows user names
  - user profile path segments such as `C:\Users\<name>`
  - email addresses
- Ollama is treated as local and does not apply redaction.
- Redaction keeps useful diagnostic context such as Event ID, provider name, timestamps, computer name, IP addresses, domain/workgroup, and hardware information.
- AI suggestions are advisory only. WinSentryAI does not execute fixes automatically.

## Portable Design

WinSentryAI is intended to run as a portable Windows tool.

Runtime files are created next to the executable:

| Data | Location |
|---|---|
| Settings | `settings.ini` |
| Database | `WinSentryAI.db` |
| Logs | `logs\app-YYYYMMDD.log` |
| API keys | SQLite `AppSettings`, encrypted via DPAPI |

Recommended locations:

- `C:\Tools\WinSentryAI\`
- a USB drive or portable tools folder

Avoid placing it under `C:\Program Files\` during development or portable use because Windows virtualization may redirect writes.

## Requirements

- Windows 10 or later
- Windows 11 supported
- Windows Server with Desktop Experience supported
- .NET 8 Desktop Runtime for framework-dependent builds
- Administrator privileges recommended for full Event Log access
- Minimum practical screen/window size: 1280 x 720
- Recommended screen/window size: 1366 x 768 or larger; 1440 x 900 or larger is more comfortable

Server Core and Nano Server are not supported because WinSentryAI is a WPF desktop application.

## Build

Install the .NET 8 SDK, then run:

```powershell
dotnet build -c Debug -p:Platform=x64
```

The fixed build output directory is:

```text
bin\build\
```

The current development build is focused on **Windows x64**. Other platform targets are not included at this stage.

## Publish

Framework-dependent portable build:

```powershell
dotnet publish -c Release -p:Platform=x64 --no-self-contained -o .\bin\publish
```

Self-contained portable build:

```powershell
dotnet publish -c Release -p:Platform=x64 --self-contained -r win-x64 -o .\bin\publish
```

No official release package is published yet for v0.5.

## Development Status

v0.5 is a functional development milestone. Core local diagnostics, AI analysis, system tray behavior, remote retrospective querying, and single-instance behavior are working, but UI polish and official release packaging are not final.

Recently completed:

- multi-provider AI flow
- follow-up chat
- system info view
- onboarding wizard with AI setup skip
- privacy redaction
- system tray behavior
- official app/tray icon resources
- remote Event Log connection flow
- remote event AI analysis fix
- single-instance launch behavior
- maintenance actions
- fixed build output layout

Known pending work:

- improve Settings UI visual quality
- refine shell, side navigation, and status bar styling
- improve AI analysis panel layout
- test remote Event Log mode in real office/network environments
- improve remote connection error messages
- validate OpenAI, Claude, and Ollama end-to-end once keys/models are available
- prepare official release packaging

## Security Notes

- The repository is configured to exclude `settings.ini`, `.db` files, logs, local AI workspace folders, and personal tool configuration.
- API keys are stored locally and encrypted with the current Windows user account.
- Moving the portable folder to another Windows user account requires re-entering API keys.
- AI responses should be reviewed by IT personnel before taking action.

## License

MIT License is planned for this project.
