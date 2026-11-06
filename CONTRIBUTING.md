# Contributing to DRIVELY

Thanks for helping. DRIVELY aims to be the simplest, safest and most transparent driver manager for Windows. Every change is judged against that goal.

## Ground rules

1. **Don't fabricate anything.** No made-up update data, version numbers, manufacturer APIs or download URLs. If something can't be determined, show that it's unknown.
2. **No new network endpoints** without discussing it in an issue first. Every endpoint must be official, use HTTPS or a Windows-managed channel, and be listed in Settings → Privacy & network.
3. **The user stays in control.** Nothing that changes the system (install, restore, delete) may run without an explicit confirmation.
4. **Never weaken Windows security.** No disabling signature enforcement, no test-signing, no running unsigned installers.
5. **Out of scope:** accounts, ads, telemetry, background services, registry cleaners, "optimizers" and AI recommendations.

## Development setup

- Windows 10/11 x64
- .NET 10 SDK
- Any editor: Visual Studio 2022 17.14+, Rider or VS Code with C# Dev Kit

```powershell
dotnet build Drively.sln
dotnet test Drively.sln
dotnet run --project src\Drively.App -- --demo
```

Use `--demo` for UI work. It runs on simulated hardware and never installs anything.

> ⚠️ **Never test real driver installation or restore on your main machine.** Use a virtual machine (Hyper-V or VirtualBox) or a spare PC with a recent system restore point.

## Project layout

| Project | Responsibility |
|---|---|
| `Drively.Core` | Models, interfaces and platform-independent logic (version comparison, INF parsing, compatibility rules) |
| `Drively.DriverDetection` | Read-only hardware and driver detection (WMI), plus the mock provider |
| `Drively.DriverUpdates` | Windows Update provider, update checking, manufacturer support links |
| `Drively.DriverInstallation` | Signature verification, package validation, pnputil, install workflow |
| `Drively.Backup` | Backup and restore |
| `Drively.Data` | SQLite history and settings |
| `Drively.App` | WPF UI (MVVM with CommunityToolkit.Mvvm) |

Dependencies point inward: UI → feature libraries → Core. Feature libraries must not reference `Drively.App`.

## Code style

- C# latest, nullable enabled, file-scoped namespaces (see `.editorconfig`).
- Keep classes small and testable. Put Windows APIs behind interfaces in `Drively.Core.Interfaces`.
- Error messages are written for non-technical users. Technical details go to the local log.
- Add or update tests for every behavior change. Tests must not modify real drivers. Use the fakes in `tests/Drively.Integration.Tests/TestDoubles.cs`.

## Translations

UI text currently lives in the XAML views. Moving it into resource files (`.resx`) is a welcome first contribution and will enable translations.

## Commits & pull requests

- Use meaningful, imperative commit messages, e.g. `Add Realtek vendor ID to support links`.
- Keep each PR focused on one change, and describe how you tested it. Say whether you tested on Windows 10, Windows 11 or both.
- `.\build.ps1` must pass.
