using System.Text.Json;
using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.DriverInstallation;

namespace Drively.Backup;

/// <summary>
/// Exports third-party driver packages (oemNN.inf) with the built-in Windows PnPUtil tool and writes
/// a JSON manifest. A driver backup is NOT a system image and does not guarantee recovery from every
/// hardware or Windows problem.
/// </summary>
public sealed class BackupManager(
    IDriverStoreReader driverStore,
    PnpUtilClient pnpUtil,
    IElevationService elevation,
    IHistoryRepository history,
    ILog log)
{
    public const string ManifestFileName = "drively-backup.json";

    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<DriverPackageInfo> GetExportableDrivers() => driverStore.GetThirdPartyPackages();

    public async Task<BackupResult> CreateBackupAsync(
        string destinationRoot,
        IReadOnlyList<DeviceInfo> devices,
        SystemInfo? system,
        string appVersion,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!elevation.IsElevated)
            return new BackupResult(false, "Administrator permissions are required to export drivers.", null);

        var packages = driverStore.GetThirdPartyPackages();
        if (packages.Count == 0)
            return new BackupResult(false, "No third-party drivers were found to back up. Drivers included with Windows are restored by Windows itself.", null);

        string folder;
        try
        {
            Directory.CreateDirectory(destinationRoot);
            folder = Path.Combine(destinationRoot, $"DRIVELY-Backup-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            log.Error("Could not create backup folder.", ex);
            return new BackupResult(false, $"The backup folder could not be created: {ex.Message}", null);
        }

        var manifest = new BackupManifest
        {
            CreatedAt = DateTimeOffset.Now,
            AppVersion = appVersion,
            WindowsVersion = system?.WindowsVersion ?? "",
            ComputerModel = system is null ? "" : $"{system.Manufacturer} {system.Model}".Trim(),
        };

        for (var i = 0; i < packages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var package = packages[i];
            progress?.Report($"Exporting {package.PublishedName} ({i + 1} of {packages.Count})…");

            var relative = Path.GetFileNameWithoutExtension(package.PublishedName);
            var target = Path.Combine(folder, relative);
            var entry = new BackupEntry
            {
                PublishedInf = package.PublishedName,
                Provider = package.Provider,
                Version = package.Version,
                DriverDate = package.Date,
                Class = package.Class,
                Folder = relative,
                DeviceNames = devices
                    .Where(d => string.Equals(d.Driver?.InfName, package.PublishedName, StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.Name)
                    .Distinct()
                    .ToList(),
            };

            try
            {
                var result = await pnpUtil.ExportDriverAsync(package.PublishedName, target, cancellationToken).ConfigureAwait(false);
                var inf = Directory.Exists(target) ? Directory.EnumerateFiles(target, "*.inf").FirstOrDefault() : null;
                entry.Exported = result.ExitCode == 0 && inf is not null;
                entry.OriginalInf = inf is null ? "" : Path.GetFileName(inf);
                if (!entry.Exported) entry.Error = $"Export failed (pnputil exit code {result.ExitCode}).";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                entry.Exported = false;
                entry.Error = $"Export failed: {ex.Message}";
            }

            if (!entry.Exported) log.Warn($"Backup: {package.PublishedName}: {entry.Error}");
            manifest.Entries.Add(entry);
        }

        await File.WriteAllTextAsync(Path.Combine(folder, ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);

        var exported = manifest.Entries.Count(e => e.Exported);
        var failed = manifest.Entries.Count - exported;
        var success = exported > 0;
        var message = failed == 0
            ? $"Backup complete: {exported} driver package{(exported == 1 ? "" : "s")} exported."
            : $"Backup finished with problems: {exported} exported, {failed} failed.";

        await RecordAsync(new HistoryEntry
        {
            Device = $"{packages.Count} third-party driver packages",
            Operation = HistoryOperation.Backup,
            Result = success ? HistoryResult.Succeeded : HistoryResult.Failed,
            Details = message + " " + folder,
        }).ConfigureAwait(false);

        log.Info(message);
        return new BackupResult(success, message, new BackupInfo(folder, manifest));
    }

    /// <summary>Lists DRIVELY backups (folders containing a manifest) under a directory, newest first.</summary>
    public IReadOnlyList<BackupInfo> ListBackups(string root)
    {
        if (!Directory.Exists(root)) return [];
        var backups = new List<BackupInfo>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var backup = TryLoad(dir);
            if (backup is not null) backups.Add(backup);
        }
        return backups.OrderByDescending(b => b.Manifest.CreatedAt).ToList();
    }

    /// <summary>Loads a backup folder, or returns null if it is not a DRIVELY backup.</summary>
    public BackupInfo? TryLoad(string folder)
    {
        var manifestPath = Path.Combine(folder, ManifestFileName);
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath), JsonOptions);
            return manifest is null ? null : new BackupInfo(folder, manifest);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            log.Warn($"Ignoring unreadable backup manifest in {Path.GetFileName(folder)}.", ex);
            return null;
        }
    }

    /// <summary>Deletes a backup folder. Refuses folders that are not DRIVELY backups.</summary>
    public void DeleteBackup(BackupInfo backup)
    {
        if (!File.Exists(Path.Combine(backup.FolderPath, ManifestFileName)))
            throw new InvalidOperationException("This folder is not a DRIVELY backup and will not be deleted.");
        Directory.Delete(backup.FolderPath, recursive: true);
        log.Info($"Deleted backup {backup.Name}.");
    }

    private async Task RecordAsync(HistoryEntry entry)
    {
        try
        {
            await history.AddAsync(entry).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Warn("Could not write history entry.", ex);
        }
    }
}
