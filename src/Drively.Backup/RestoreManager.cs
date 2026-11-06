using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.DriverInstallation;

namespace Drively.Backup;

/// <summary>
/// Restores driver packages from a DRIVELY backup. Each package is validated (INF, catalog, trusted
/// signature) before PnPUtil is called. Call only after the user explicitly confirmed.
/// Note: Windows chooses the best-ranked driver for each device, so restoring an older version
/// does not always replace a newer one that is already installed.
/// </summary>
public sealed class RestoreManager(DriverValidator validator, DriverInstaller installer, IHistoryRepository history, ILog log)
{
    /// <summary>Resolves the INF path for an entry, refusing paths that escape the backup folder.</summary>
    public static string? ResolveInfPath(BackupInfo backup, BackupEntry entry)
    {
        if (!entry.Exported || string.IsNullOrWhiteSpace(entry.OriginalInf) || string.IsNullOrWhiteSpace(entry.Folder)) return null;

        var root = Path.GetFullPath(backup.FolderPath);
        var path = Path.GetFullPath(Path.Combine(root, entry.Folder, entry.OriginalInf));
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    public PackageValidationResult Validate(BackupInfo backup, BackupEntry entry)
    {
        var inf = ResolveInfPath(backup, entry);
        return inf is null
            ? new PackageValidationResult { InfPath = entry.OriginalInf, Errors = ["This driver was not exported successfully or its path is invalid."] }
            : validator.Validate(inf);
    }

    public async Task<IReadOnlyList<RestoreItemResult>> RestoreAsync(
        BackupInfo backup,
        IReadOnlyList<BackupEntry> entries,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<RestoreItemResult>();
        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[i];
            progress?.Report($"Restoring {entry.OriginalInf} ({i + 1} of {entries.Count})…");

            var inf = ResolveInfPath(backup, entry);
            var result = inf is null
                ? OperationResult.Fail("This driver was not exported successfully or its path is invalid.")
                : await installer.InstallPackageAsync(inf, cancellationToken).ConfigureAwait(false);

            results.Add(new RestoreItemResult(entry, result));
            log.Info($"Restore {entry.PublishedInf}: {(result.Success ? "ok" : "failed")} - {result.Message}");

            try
            {
                await history.AddAsync(new HistoryEntry
                {
                    Device = entry.DeviceNames.Count > 0 ? string.Join(", ", entry.DeviceNames) : $"{entry.Provider} {entry.Class}".Trim(),
                    DriverVersion = entry.Version,
                    Operation = HistoryOperation.Restore,
                    Result = result.Success ? HistoryResult.Succeeded : HistoryResult.Failed,
                    Details = result.Message,
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Warn("Could not write history entry.", ex);
            }
        }
        return results;
    }
}
