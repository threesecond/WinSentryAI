# WinSentryAI Privacy Policy

WinSentryAI is a portable Windows desktop diagnostic tool. It is designed to keep diagnostic data local unless the user explicitly chooses to use a cloud AI provider.

## Local Data

WinSentryAI stores runtime data next to the executable:

- `settings.ini` for non-secret settings.
- `WinSentryAI.db` for event records, context logs, AI analysis results, system snapshots, and encrypted provider API keys.
- `logs\app-YYYYMMDD.log` for application diagnostic logs.

These files are not uploaded by WinSentryAI.

## API Keys

Cloud AI provider API keys are stored locally in SQLite and encrypted with Windows DPAPI using the current Windows user account. API keys are not stored in plaintext `settings.ini` files.

If the portable folder is moved to another Windows account or computer, encrypted keys may not decrypt and must be entered again.

## Cloud AI Providers

WinSentryAI supports Bring Your Own Key cloud AI providers such as Gemini, OpenAI, and Claude. Cloud AI requests are only made when the user configures a provider and starts an AI analysis or follow-up chat.

When a cloud provider is used, WinSentryAI may send:

- Selected Windows Event Log details.
- Related context event summaries.
- System snapshot fields useful for diagnosis.
- The user's follow-up chat questions for the selected event.

Before sending event message text to cloud AI providers, WinSentryAI applies privacy redaction for high-risk personal data currently including Windows user names, `C:\Users\<name>` path segments, and email addresses. Redaction is a best-effort safety measure and may not remove every sensitive value from every possible log format.

Ollama is treated as a local provider. WinSentryAI sends requests to the configured Ollama endpoint and does not apply cloud redaction for Ollama.

## Remote Connections

Remote Event Log mode reads Windows Event Logs from a remote computer using Windows Event Log APIs. Remote passwords are used for the current connection attempt and are not saved by WinSentryAI.

The remote setup script enables Windows services and firewall rules on the remote computer. Use it only on trusted networks and only on computers you administer.

## Telemetry And Updates

WinSentryAI does not include telemetry, analytics, or automatic update checks in v0.9.0.

## User Control

Users can avoid cloud data transfer by not configuring cloud AI providers or by using Ollama locally. Users can delete local runtime data by removing the portable application folder or using in-app maintenance features where available.

## Contact

Please report privacy issues through the GitHub repository issue tracker.
