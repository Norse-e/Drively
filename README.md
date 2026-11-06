# DRIVELY

**A free, open-source Windows driver manager. No accounts, ads, subscriptions or tracking.**

DRIVELY shows the hardware in your PC and the drivers it uses, and points out devices with problems or missing drivers. It finds verified driver updates through Microsoft Windows Update, installs them only after you confirm, and backs up and restores your third-party drivers.

## Features

| | |
|---|---|
| **Hardware & driver inventory** | Every Plug and Play device, with its manufacturer, hardware ID, class, driver provider, version, date, INF, signer and status. Devices are grouped into categories (graphics, audio, network, Bluetooth, chipset, storage, USB, input, monitors, other). |
| **Problem detection** | Missing drivers (code 28), disabled devices (22) and device errors, each explained in plain language. |
| **Verified updates** | Drivers come from Microsoft Windows Update. An update is shown only if its hardware ID matches a device in this PC and its version is newer than the one installed. When the update would replace a driver from another provider, DRIVELY says so. |
| **Safe installation** | *Download* or *Download & Install*, always behind a confirmation dialog. Installation goes through the Windows Update Agent, and Windows enforces driver signatures. A restart is never forced. |
| **Backup & restore** | Exports third-party drivers with `pnputil`, writes a JSON manifest, and restores selected drivers after validating the INF and catalog signature. |
| **History** | A local SQLite log of downloads, installs, backups and restores, which you can clear. |
| **Official support links** | For each device, links to the official NVIDIA, AMD, Intel, Realtek, ASUS, MSI, Gigabyte, Lenovo, Dell, HP or Acer support page. |
| **Inventory export** | CSV or JSON. |
| **Light / dark / system theme** | |

## What DRIVELY does *not* do

- It doesn't install anything automatically and doesn't run in the background.
- It doesn't download drivers from unofficial driver sites or databases.
- It doesn't turn off driver signature enforcement or any other Windows security feature.
- It doesn't clean the registry or "optimize" your system.
- It doesn't show estimated or invented update counts. Every number on screen comes from detection on your PC.

## Supported Windows versions

- Windows 10 (64-bit), version 1809 or later
- Windows 11 (64-bit)

Architecture: x64.

## Installation

1. Download `DRIVELY-win-x64.zip` from the project's Releases page.
2. Check the SHA-256 hash against `SHA256SUMS.txt`.
3. Extract the zip and run `Drively.exe`. No installer is needed, and the .NET runtime is included.

DRIVELY runs as a standard user. Installing, backing up and restoring drivers need administrator rights. DRIVELY explains why and has a **Restart as administrator** button.

To try the interface without your real hardware, run `Drively.exe --demo`. This uses clearly labelled, simulated "Contoso" devices, and nothing is installed.

## Building from source

Requirements: Windows 10/11 x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone <repository-url>
cd DRIVELY
.\build.ps1             # build + run all tests
.\build.ps1 -Publish    # also produce artifacts\DRIVELY-win-x64.zip (self-contained, single file)
```

To run from source:

```powershell
dotnet run --project src\Drively.App            # real hardware
dotnet run --project src\Drively.App -- --demo  # simulated hardware
```

## Privacy

- Driver information, history, settings and logs are stored only in `%LOCALAPPDATA%\Drively`.
- The only network connection is to **Microsoft Windows Update**, made through the built-in Windows Update Agent, and only when you check for, download or install updates. You can optionally turn on an update check at startup.
- Manufacturer support pages open in your browser only when you click a link.
- No analytics, telemetry, accounts or cloud sync.

## Known limitations

- **Update coverage depends on what Windows Update has.** Drivers that NVIDIA, AMD, Intel and others publish only on their own websites aren't discovered automatically. These vendors offer no public, supported API for driver updates, so DRIVELY links to their official support pages and doesn't scrape them.
- **Windows Update titles don't always include a version number.** When one is missing, DRIVELY shows "Not reported by source" and doesn't guess.
- **Driver ranking is Windows' decision.** A restore re-adds a driver package, but Windows keeps whichever driver it ranks best, so an older backed-up version doesn't always replace a newer one.
- **Backups contain only third-party drivers** (`oemNN.inf`). Inbox Windows drivers are restored by Windows itself. **A driver backup is not a system image.**
- **Packages from Windows Update are downloaded into Windows' own update cache,** not into DRIVELY's download directory. That directory is used for inventory exports.
- The interface is in English only for now. Translations are welcome.
- Some PCs have a Group Policy that blocks drivers from Windows Update (`ExcludeWUDriversInQualityUpdate`). DRIVELY detects this and tells you.

## Documentation

- [Architecture & design decisions](docs/ARCHITECTURE.md)
- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

## License

[MIT](LICENSE)
