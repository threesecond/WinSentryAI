# WinSentryAI

**WinSentryAI** is a portable Windows desktop diagnostic tool for reviewing Windows Event Logs and using AI to help IT operators understand system anomalies.

Current version: **v0.9**  
Status: **release candidate**

Languages: [English](README.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md)

## Overview

WinSentryAI is designed for hands-on Windows troubleshooting. It combines Event Log entries, system snapshots, related context events, and optional AI analysis in a desktop workflow.

It is intended to help IT personnel inspect issues faster while keeping remediation decisions under human control.

## Key Features

- Windows Event Log retrospective loading and local monitoring
- Event filtering, search, details, and context event capture
- AI analysis and follow-up chat for selected events
- Bring Your Own Key support for Gemini, OpenAI, Claude, and Ollama
- Privacy redaction for cloud AI prompts
- System information snapshot
- Remote Windows Event Log retrospective query
- System tray behavior and single-instance launch handling
- English, Traditional Chinese, and Simplified Chinese UI
- Light, Dark, and System theme options

## Screenshots

The Event Log view lets operators filter Windows events, inspect details, and start AI analysis from the selected entry.

![Event Log view](Docs/Images/event-log-light.png)

AI analysis provides an event-focused explanation with a follow-up chat for asking practical remediation questions.

![AI analysis follow-up chat](Docs/Images/ai-analyze-light.png)

The settings page can fetch available Gemini models from the configured API key and select the model used for analysis.

![Gemini model selection](Docs/Images/gemini-api-light.png)

AI provider settings support Gemini, OpenAI, Claude, and Ollama with local privacy redaction options.

![AI provider settings](Docs/Images/settings-ai-provider.light.png)

## Documentation

Operational details are maintained in the user manual:

- [User Manual](Docs/user-manual.md)
- [使用手冊（繁體中文）](Docs/user-manual.zh-TW.md)
- [用户手册（简体中文）](Docs/user-manual.zh-CN.md)

The manual covers installation requirements, build and run instructions, API key setup, AI provider limits, remote connection setup, tray behavior, theme settings, known limitations, and troubleshooting notes.

## Scope

WinSentryAI provides diagnostic assistance only. It does not automatically repair systems, modify Event Logs, or execute remediation commands based on AI output.

AI results should be reviewed by qualified IT personnel before taking action.

## License

MIT License is planned for this project.
