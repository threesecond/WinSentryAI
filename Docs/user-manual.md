# WinSentryAI User Manual

Version: v0.9

## 1. What WinSentryAI Is

WinSentryAI is a portable Windows desktop tool for reading Windows Event Logs and requesting AI-assisted diagnostic summaries. It is intended for IT operators who already understand Windows administration basics.

WinSentryAI does not automatically repair systems. AI output is advisory and must be reviewed before any operational action.

## 2. Installation Requirements

- Windows 10 or later
- Windows 11 supported
- Windows Server with Desktop Experience supported
- Windows x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-US/download/dotnet/8.0) for framework-dependent builds
- Administrator privileges recommended for full Event Log access
- Minimum practical screen/window size: 1280 x 720
- Recommended screen/window size: 1366 x 768 or larger

Server Core and Nano Server are not supported because WinSentryAI is a WPF desktop application.

Recommended portable locations:

- `C:\Tools\WinSentryAI\`
- USB drive or portable tools folder

Avoid `C:\Program Files\` for portable use because Windows write virtualization may redirect local settings and database files.

## 3. Build And Run

Install the [.NET 8 SDK](https://dotnet.microsoft.com/en-US/download/dotnet/8.0), then build:

```powershell
dotnet build -c Debug -p:Platform=x64
```

The fixed build output directory is:

```text
bin\build\
```

Run the application from the build output:

```powershell
.\bin\build\WinSentryAI.exe
```

Framework-dependent publish:

```powershell
dotnet publish -c Release -p:Platform=x64 --no-self-contained -o .\bin\publish
```

Self-contained publish:

```powershell
dotnet publish -c Release -p:Platform=x64 --self-contained -r win-x64 -o .\bin\publish
```

## 4. Runtime Files

WinSentryAI stores runtime files next to the executable:

| Data | Location |
|---|---|
| Settings | `settings.ini` |
| Database | `WinSentryAI.db` |
| Logs | `logs\app-YYYYMMDD.log` |
| API keys | SQLite `AppSettings`, encrypted via Windows DPAPI |

API keys are encrypted for the current Windows user account. If the portable folder is moved to another Windows account, API keys must be entered again.

## 5. First Launch

On first launch, WinSentryAI shows an onboarding flow for language and AI provider setup. AI setup can be skipped and completed later in Settings.

Running as Administrator is recommended. Without administrator privileges, some logs, especially Security logs, may be unavailable.

## 6. API Key Setup

WinSentryAI uses a Bring Your Own Key model.

Open **Settings > AI Settings**, choose a provider, enter the model name and API key, then save.

Cloud API keys are stored locally and encrypted with Windows DPAPI. They are not stored in plaintext settings files.

Provider API key entry points:

- Gemini: https://ai.google.dev/gemini-api/docs/api-key?hl=en
- OpenAI: https://platform.openai.com/api-keys
- Claude: https://console.anthropic.com/

## 7. AI Provider Free / Paid Notes

Free quotas, trial credits, available models, and rate limits can change. Always confirm current status in the provider console or billing page.

### Gemini

Gemini API documentation lists Free Tier and Paid Tier options. It is a practical first choice for low-cost WinSentryAI testing.

- Pricing / Free Tier: https://ai.google.dev/gemini-api/docs/pricing?hl=en
- Billing: https://ai.google.dev/gemini-api/docs/billing?hl=en
- API key: https://ai.google.dev/gemini-api/docs/api-key?hl=en
- Google AI Studio: https://aistudio.google.com/

### OpenAI

OpenAI API usage is primarily usage-based. Trial credits or free usage depend on current account and billing status.

- Pricing: https://platform.openai.com/docs/pricing/
- API docs: https://platform.openai.com/docs/
- API keys: https://platform.openai.com/api-keys
- Billing: https://platform.openai.com/settings/organization/billing/overview
- Prepaid billing help: https://help.openai.com/en/articles/8264778-what-is-prepaid-billing

### Claude

Claude API keys and usage credits are managed through the Anthropic Console. Actual credit availability must be confirmed in Console or Billing.

- Pricing: https://docs.anthropic.com/en/docs/about-claude/pricing
- Get started: https://docs.anthropic.com/en/docs/get-started
- API overview: https://docs.anthropic.com/en/api/getting-started
- Console: https://console.anthropic.com/
- Billing / usage credits help: https://support.anthropic.com/en/articles/8977456-how-do-i-pay-for-my-api-usage

### Ollama

Ollama runs locally and does not require a cloud API key. It requires the Ollama service and a local model to be installed separately.

Limitations:

- Model quality and speed depend on the local machine.
- Large models may require significant RAM/VRAM.
- WinSentryAI can only use models available from the configured Ollama endpoint.
- Local mode avoids cloud API usage but does not guarantee better diagnostic quality.

## 8. Privacy And Cloud AI

For cloud AI providers, WinSentryAI can redact high-risk personal data before sending the prompt:

- Windows user names
- user profile path segments such as `C:\Users\<name>`
- email addresses

Redaction keeps diagnostic context such as Event ID, provider name, timestamps, computer name, IP addresses, domain/workgroup, and hardware information.

Ollama is treated as local and does not apply cloud redaction.

## 9. Remote Connection Setup

Remote connection is for retrospective Windows Event Log queries. It is not real-time remote monitoring.

Remote setup requires preparation on the remote Windows computer.

### Security Notice

The setup script enables RPC/DCOM, Remote Event Log Management, WMI firewall rules, TCP 135, and the Remote Registry service. Use it only on trusted networks and only on computers you administer.

By default, the script applies firewall rules to Domain and Private profiles. Avoid enabling the Public profile unless your environment requires it and has additional network protection.

### Remote Account Requirements

The account used to connect to the remote computer must be a member of these local groups on the remote computer:

- Administrators
- Event Log Readers

If you connect with a domain account, add that domain account or a domain group containing that account to both local groups on the remote computer.

### Script Location

The project includes:

```text
Scripts\EnableWindowsEventLogViewerPolicy.ps1
```

Copy this file to the remote computer and run it from an elevated PowerShell session.

### Running The Script

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\EnableWindowsEventLogViewerPolicy.ps1
```

If Windows blocks the script because it came from another computer:

```powershell
Unblock-File .\EnableWindowsEventLogViewerPolicy.ps1
```

Process-only execution policy alternative:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\EnableWindowsEventLogViewerPolicy.ps1
```

Public firewall profile, only when required:

```powershell
.\EnableWindowsEventLogViewerPolicy.ps1 -IncludePublicProfile
```

### If Remote Connection Fails

Check:

- Remote account is in Administrators and Event Log Readers on the remote computer.
- Host name or IP address resolves correctly.
- Windows Firewall or third-party firewall allows RPC/Event Log traffic.
- Remote computer is reachable from the WinSentryAI computer.
- Domain/workgroup credentials are correct.
- Security log access may require administrator privileges.

## 10. Tray Behavior

When the main window is closed, WinSentryAI can remain running in the system tray and continue local monitoring. The first tray hint explains this behavior.

Common behavior:

- Double-click the tray icon to restore the main window.
- Use the tray context menu to show or exit the application.
- Launching WinSentryAI again brings the existing instance forward instead of starting a second instance.

## 11. Theme Settings

Settings provides:

- `Light`
- `Dark`
- `System`

Theme changes apply after saving settings. Some input and dropdown controls intentionally use white backgrounds for readability across both Light and Dark modes.

Known theme limitation:

- Settings dropdowns currently prioritize readability with white backgrounds instead of fully native dark styling.

## 12. Known Issues And Limitations

- Remote connection is retrospective querying, not real-time remote monitoring.
- Security log access may require Administrator privileges.
- Cloud AI sends redacted event summaries to the selected AI provider.
- API usage may generate provider costs.
- Ollama local mode depends on installed local models and machine performance.
- Theme dropdowns currently use white backgrounds for readability.
- AI output can be incomplete or incorrect and must be reviewed by IT personnel.
- v0.9 is a release candidate. Official stable release packaging is still in progress.
