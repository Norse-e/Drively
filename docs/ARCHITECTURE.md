# DRIVELY architecture

## Overview

```
┌──────────────────────────── Drively.App (WPF, MVVM) ────────────────────────────┐
│ Views (XAML) ─ ViewModels ─ DriverSession (shared state) ─ Theme/Dialog/Elevation │
└──────┬──────────────┬────────────────┬───────────────────┬──────────────┬────────┘
       │              │                │                   │              │
 DriverDetection  DriverUpdates   DriverInstallation      Backup         Data
 WMI PnP scan     WUA search      INF + catalog check     pnputil        SQLite
 driver store     compatibility   WinVerifyTrust          export /       history,
 mock provider    vendor links    pnputil, WUA install    restore        settings
       └──────────────┴──────── Drively.Core (models · interfaces · rules) ┴───────┘
```

All Windows-specific code sits behind interfaces in `Drively.Core.Interfaces`, so the UI and logic can be tested with fakes or with the `--demo` mock providers.

## Main flows

**Scan** (`IHardwareProvider` → `WmiHardwareProvider`)
1. `HardwareScanner` queries `Win32_PnPEntity` for instance ID, name, manufacturer, PNPClass, problem code, hardware IDs and compatible IDs.
2. `DriverScanner` queries `Win32_PnPSignedDriver` for provider, version, date, INF and signer, keyed by device ID.
3. `DeviceIdentifier` joins the two, categorizes each device by setup class, maps problem codes to states, and parses the PCI/USB vendor ID.

A typical PC scans in about 4 seconds. There's no background or repeated scanning.

**Update check** (`UpdateChecker`)
1. `INetworkStatus` checks the local adapters first, with no network request. If offline, the result is "No internet connection."
2. `WindowsUpdateProvider` searches with `IsInstalled=0 and Type='Driver' and IsHidden=0`, using the late-bound COM API `Microsoft.Update.Session`.
3. `CompatibilityEvaluator` keeps an offer only if its hardware ID exactly matches a device in the PC and the offered version is strictly newer (when both versions are known). Driver dates are never used.
4. If the search fails, the check fails with a reason instead of reporting "no updates".

**Install** (`InstallationManager` → `IUpdateProvider`)
UI confirmation → elevation check → WUA `UpdateDownloader` → WUA `UpdateInstaller` → result codes mapped to honest messages → history entry. A required restart is reported and never forced.

**Backup / restore** (`BackupManager`, `RestoreManager`)
- Backup: read `%windir%\INF\oem*.inf`, run `pnputil /export-driver oemN.inf <backup>\oemN` for each, and write `drively-backup.json`.
- Restore: for each selected entry, resolve its path (refusing traversal), run `DriverValidator` (INF, catalog in folder, WinVerifyTrust), then `pnputil /add-driver <inf> /install`.

## Design decisions

| Decision | Rationale |
|---|---|
| .NET 10 (LTS) instead of .NET 8 | .NET 8 support ends in Nov 2026. .NET 10 is the current LTS. |
| Windows Update Agent as the only automatic update source | It's the only public, supported, signed and hash-verified driver distribution channel. NVIDIA, AMD, Intel, Realtek and the OEMs publish no supported public API for driver updates, and scraping their sites would be fragile and could break their terms. |
| Manufacturer *links* instead of manufacturer downloads | This follows the spec's "do not invent manufacturer APIs or URLs". The links point to official support pages on vendor domains (checked 2026-11-06: NVIDIA, Intel, ASUS and Dell loaded; MSI, Gigabyte, Lenovo and Acer block automated requests with 403; AMD timed out; HP returned an empty page to automated requests). |
| Late-bound COM (`dynamic`) for WUA | `COMReference` isn't supported by the .NET SDK, and hand-written vtable interop for WUA is error-prone. The dynamic calls were verified against the real API on Windows 11 25H2. |
| WUA download cache instead of a custom download directory | Windows handles content hash verification and cleanup. A custom downloader would duplicate security-critical work. |
| `asInvoker` + "Restart as administrator" | Admin rights are only requested when the user wants them. No elevated service is installed. |
| WMI instead of SetupAPI for enumeration | WMI exposes everything required (hardware/compatible IDs, problem code, driver details) without unsafe P/Invoke. SetupAPI can be added later behind `IHardwareProvider` if needed. |
| Reading `oem*.inf` for the backup list | `pnputil /enum-drivers` output is localized and unreliable to parse. The published INFs are the source of truth and include packages not bound to a connected device. |
| Microsoft.Data.Sqlite without EF Core | Two small tables don't justify an ORM. Schema versions are tracked with `PRAGMA user_version`. |
| Settings in SQLite key/value | As required by the spec. Backup metadata is JSON so a backup folder describes itself on any PC. |
| Custom WPF styles, no UI framework | Fewer dependencies and a fast startup. Light/dark themes are swappable `ResourceDictionary`s, and title bars are themed through DWM. |

## Roadmap

- **v0.1 (this MVP):** everything above.
- **v0.2:** move UI strings to `.resx` and add translations, optional Windows toast after a manual check, SetupAPI fallback for detection, per-device "hide this update".
- **v0.3:** signed releases (Authenticode + winget manifest), a CI pipeline running tests on Windows 10 and 11 runners, accessibility review (screen reader, high contrast).
- **Later:** "install driver package from folder" (reusing `DriverInstaller`), and a system restore point before installs when System Protection is enabled.

## Testing strategy

- **Unit tests** (`Drively.Core.Tests`, `Drively.DriverDetection.Tests`): version comparison, INF parsing, categorization, compatibility rules, CSV/JSON export, device identification, mock provider.
- **Integration tests** (`Drively.Integration.Tests`): real read-only WMI scan and system info, real WinVerifyTrust on Windows catalogs (trusted and untrusted), package validation, SQLite round trips, backup/restore and install error handling using fake `pnputil` and fake update providers.
- **Never** run destructive install or restore tests on a developer's main machine. Use a VM.
