# Security policy

DRIVELY installs kernel-mode software (drivers), so security is its top priority.

## Reporting a vulnerability

Please **do not open a public issue** for security problems. Use the repository's private vulnerability reporting (GitHub → *Security* → *Report a vulnerability*) and include steps to reproduce. You should hear back within 7 days.

## Security design

| Area | Measure |
|---|---|
| **Driver sources** | Only Microsoft Windows Update, used through the Windows Update Agent API. Packages are Microsoft-signed, Windows verifies their content hashes, and Windows decides whether each one applies. DRIVELY never downloads drivers from third-party sites. |
| **Compatibility** | An update is shown only when its hardware ID exactly matches a device in this PC, and it's hidden if the installed version is the same or newer. |
| **Signatures** | Before a local package (restore) is installed, DRIVELY parses its INF, checks that the catalog lies inside the package folder, and verifies the catalog with `WinVerifyTrust`. Revocation uses cached data only, so the check makes no network calls. Windows then enforces its own driver signature policy, and DRIVELY never bypasses it. |
| **Installation** | Only supported mechanisms are used: the Windows Update Agent installer and `pnputil /add-driver /install`. No vendor `.exe` installers are ever run. |
| **Consent** | Every install, restore and delete needs an explicit confirmation. Destructive dialogs default to *Cancel*. |
| **Privileges** | DRIVELY runs as a standard user (`asInvoker`) and elevates only when you choose *Restart as administrator*. There is no service and no scheduled task. |
| **Process execution** | `pnputil.exe` is started from `%SystemRoot%\System32` with an argument list, never through a shell. Published names must match `^oem\d+\.inf$`, and INF paths must be fully qualified. |
| **Path safety** | Paths inside backup manifests are resolved and refused if they point outside the backup folder. Only folders that contain a DRIVELY manifest can be deleted. |
| **Links** | Only `https://` and `ms-settings:` links can be opened. |
| **Exports** | CSV exports neutralize spreadsheet formula injection. |
| **Data** | Everything stays local in `%LOCALAPPDATA%\Drively`. There's no telemetry. Logs contain device names and driver versions, never personal data. |
| **Dependencies** | Only Microsoft / .NET Foundation packages and SQLitePCLRaw, pinned centrally in `Directory.Packages.props`. NuGet vulnerability auditing is enabled in the build. |

## Supported versions

Only the latest release receives security fixes.
