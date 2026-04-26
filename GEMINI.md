# WinSentryAI — Gemini CLI Project Context

**All project specifications, architecture decisions, coding conventions, and development roadmap are defined in [`CLAUDE.md`](./CLAUDE.md).**

Read `CLAUDE.md` in full before starting any task. It is the single source of truth for this project.

## Quick Reference

- **Tech Stack**: C# / .NET 8 / WPF + HandyControl
- **Pattern**: MVVM (CommunityToolkit.Mvvm)
- **Database**: SQLite (Microsoft.Data.Sqlite)
- **Current Phase**: MVP — project skeleton exists, implementation not yet started
- **Version**: v0.1.0

## Current File Structure

```
WinSentryAI/
├── App.xaml / App.xaml.cs         ← Application entry point (skeleton)
├── MainWindow.xaml / .cs          ← Main window (skeleton)
├── WinSentryAI.csproj             ← All NuGet packages already configured
├── app.manifest                   ← requireAdministrator + DPI awareness
├── CLAUDE.md                      ← Full project spec (READ THIS FIRST)
├── *-mockup.html                  ← UI design references (8 files)
└── .ai/development-notes-*.md     ← Daily dev logs
```

## Where to Start (MVP Phase)

1. Create folder structure: `Views/`, `ViewModels/`, `Models/`, `Services/`, `Resources/Strings/`
2. `Services/DatabaseService.cs` — SQLite init + schema creation
3. `App.xaml.cs` — Serilog setup, i18n init, AppUserModelId registration
4. `Services/SettingsService.cs` — `settings.ini` read/write
5. Main window navigation skeleton

Refer to `CLAUDE.md` sections: **SQLite Schema**, **Application Debug Log**, **多語言 (i18n) 架構**, **設定檔架構**.
