# Third-Party Notices

This document lists third-party packages and notable runtime components used by WinSentryAI v0.9.0. It is based on the current `WinSentryAI.csproj`, NuGet metadata, and published portable package contents. Re-check this file when dependencies are added, removed, or upgraded.

WinSentryAI itself is licensed under the MIT License.

## Direct NuGet Dependencies

| Package | Version | License | Project |
|---|---:|---|---|
| CommunityToolkit.Mvvm | 8.4.0 | MIT | https://github.com/CommunityToolkit/dotnet |
| HandyControl | 3.5.1 | MIT | https://github.com/HandyOrg/HandyControl |
| Hardcodet.NotifyIcon.Wpf | 2.0.1 | MIT | https://github.com/hardcodet/wpf-notifyicon |
| Markdig | 1.1.3 | BSD-2-Clause | https://github.com/xoofx/markdig |
| Microsoft.Data.Sqlite | 10.0.7 | MIT | https://github.com/dotnet/efcore |
| Microsoft.Extensions.Logging | 8.0.1 | MIT | https://github.com/dotnet/runtime |
| Serilog | 4.2.0 | Apache-2.0 | https://serilog.net/ |
| Serilog.Extensions.Logging | 8.0.0 | Apache-2.0 | https://github.com/serilog/serilog-extensions-logging |
| Serilog.Sinks.File | 6.0.0 | Apache-2.0 | https://github.com/serilog/serilog-sinks-file |
| System.Management | 8.0.0 | MIT | https://github.com/dotnet/runtime |

## Notable Transitive / Runtime Dependencies

| Component | Version | License | Notes |
|---|---:|---|---|
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.11 | Apache-2.0 | Bundles SQLitePCLRaw provider setup for e_sqlite3. |
| SQLitePCLRaw.core | 2.1.11 | Apache-2.0 | SQLitePCLRaw core package. |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.11 | Apache-2.0 | SQLitePCLRaw provider package. |
| SQLite / e_sqlite3 native library | 2.1.11 package line | SQLite public domain / upstream package notices | Native SQLite library distributed through SQLitePCLRaw packages. |
| Microsoft.Extensions.DependencyInjection | runtime dependency | MIT | Included through Microsoft.Extensions packages. |
| Microsoft.Extensions.Options | runtime dependency | MIT | Included through Microsoft.Extensions packages. |
| Microsoft.Extensions.Primitives | runtime dependency | MIT | Included through Microsoft.Extensions packages. |

## Assets

WinSentryAI project icons under `Resources/Icons/` are project assets for this repository.

Screenshots under `Docs/Images/` are generated from WinSentryAI application views and are included for documentation.

## License Texts

The canonical license texts are available from the upstream projects and NuGet package metadata. Common license references:

- MIT License: https://opensource.org/license/mit/
- BSD 2-Clause License: https://opensource.org/license/bsd-2-clause/
- Apache License 2.0: https://www.apache.org/licenses/LICENSE-2.0

If a license notice is missing or inaccurate, please open an issue so it can be corrected.
