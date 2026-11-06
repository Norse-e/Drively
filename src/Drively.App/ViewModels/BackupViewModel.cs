using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Drively.App.Services;
using Drively.Backup;
using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.App.ViewModels;

public sealed partial class BackupEntryItem(BackupEntry entry) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = entry.Exported;

    public BackupEntry Entry { get; } = entry;
}

/// <summary>Backup &amp; Restore page.</summary>
public sealed partial class BackupViewModel : ObservableObject, INavigationAware
{
    private readonly BackupManager _backups;
    private readonly RestoreManager _restore;
    private readonly IElevationService _elevation;
    private readonly ISettingsRepository _settings;
    private readonly DriverSession _session;
    private readonly DialogService _dialogs;
    private readonly AppEnvironment _environment;
    private readonly ILog _log;

    [ObservableProperty]
    private string _backupDirectory = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedBackup))]
    [NotifyCanExecuteChangedFor(nameof(RestoreSelectedCommand), nameof(DeleteBackupCommand), nameof(OpenBackupFolderCommand))]
    private BackupInfo? _selectedBackup;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateBackupCommand), nameof(RestoreSelectedCommand), nameof(DeleteBackupCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "";

    public BackupViewModel(
        BackupManager backups,
        RestoreManager restore,
        IElevationService elevation,
        ISettingsRepository settings,
        DriverSession session,
        DialogService dialogs,
        AppEnvironment environment,
        ILog log)
    {
        _backups = backups;
        _restore = restore;
        _elevation = elevation;
        _settings = settings;
        _session = session;
        _dialogs = dialogs;
        _environment = environment;
        _log = log;
    }

    public ObservableCollection<BackupInfo> Backups { get; } = [];
    public ObservableCollection<BackupEntryItem> Entries { get; } = [];
    public bool HasSelectedBackup => SelectedBackup is not null;
    public bool IsElevated => _elevation.IsElevated;
    public bool IsDemoMode => _environment.IsDemoMode;

    public void OnNavigatedTo(object? parameter)
    {
        BackupDirectory = _settings.Load().BackupDirectory;
        RefreshBackups();
    }

    partial void OnSelectedBackupChanged(BackupInfo? value)
    {
        Entries.Clear();
        if (value is null) return;
        foreach (var entry in value.Manifest.Entries) Entries.Add(new BackupEntryItem(entry));
    }

    [RelayCommand]
    private void ChangeDirectory()
    {
        var folder = _dialogs.PickFolder("Choose where to store driver backups", BackupDirectory);
        if (folder is null) return;
        BackupDirectory = folder;
        var settings = _settings.Load();
        settings.BackupDirectory = folder;
        _settings.Save(settings);
        RefreshBackups();
    }

    [RelayCommand]
    private void OpenOtherBackup()
    {
        var folder = _dialogs.PickFolder("Open a DRIVELY backup folder", BackupDirectory);
        if (folder is null) return;
        var backup = _backups.TryLoad(folder);
        if (backup is null)
        {
            _dialogs.ShowError("Not a DRIVELY backup", $"The selected folder does not contain {BackupManager.ManifestFileName}.");
            return;
        }
        if (Backups.All(b => b.FolderPath != backup.FolderPath)) Backups.Insert(0, backup);
        SelectedBackup = Backups.First(b => b.FolderPath == backup.FolderPath);
    }

    [RelayCommand]
    private void RefreshBackups()
    {
        Backups.Clear();
        try
        {
            foreach (var backup in _backups.ListBackups(BackupDirectory)) Backups.Add(backup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Backups could not be listed: {ex.Message}";
        }
        SelectedBackup = Backups.FirstOrDefault();
    }

    private bool CanCreate() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateBackup()
    {
        if (IsDemoMode)
        {
            _dialogs.ShowInfo("Demo mode", "Backups are disabled in demo mode because the demo hardware is simulated.");
            return;
        }
        if (!EnsureElevated("Exporting drivers")) return;

        var count = _backups.GetExportableDrivers().Count;
        var ok = _dialogs.Confirm(new ConfirmRequest(
            "Create driver backup?",
            $"DRIVELY will export {count} third-party driver package{(count == 1 ? "" : "s")} with the Windows PnPUtil tool. Drivers are copied, not changed.",
            "Create backup",
            [new("Destination", BackupDirectory)],
            "A driver backup is not a complete system image and does not guarantee recovery from every hardware or Windows problem."));
        if (!ok) return;

        IsBusy = true;
        try
        {
            var progress = new Progress<string>(s => StatusText = s);
            var result = await _backups.CreateBackupAsync(BackupDirectory, _session.Scan?.Devices ?? [], _session.System,
                _environment.Version, progress);
            StatusText = result.Message;
            RefreshBackups();
            if (result.Backup is not null) SelectedBackup = Backups.FirstOrDefault(b => b.FolderPath == result.Backup.FolderPath);
            if (!result.Success) _dialogs.ShowError("Backup failed", result.Message);
        }
        catch (Exception ex)
        {
            _log.Error("Backup failed.", ex);
            StatusText = "Backup failed: " + ex.Message;
            _dialogs.ShowError("Backup failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanActOnBackup() => SelectedBackup is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanActOnBackup))]
    private async Task RestoreSelected()
    {
        if (SelectedBackup is not { } backup) return;
        var chosen = Entries.Where(e => e.IsSelected).Select(e => e.Entry).ToList();
        if (chosen.Count == 0)
        {
            _dialogs.ShowInfo("Nothing selected", "Select at least one driver in the backup contents.");
            return;
        }
        if (!EnsureElevated("Restoring drivers")) return;

        // Validate every package before asking for confirmation.
        IsBusy = true;
        StatusText = "Validating driver packages and signatures…";
        List<(BackupEntry Entry, PackageValidationResult Validation)> validated;
        try
        {
            validated = await Task.Run(() => chosen.Select(e => (e, _restore.Validate(backup, e))).ToList());
        }
        finally
        {
            IsBusy = false;
        }

        var valid = validated.Where(v => v.Validation.IsValid).Select(v => v.Entry).ToList();
        var invalid = validated.Where(v => !v.Validation.IsValid).ToList();
        StatusText = $"{valid.Count} package(s) passed validation, {invalid.Count} failed.";

        if (valid.Count == 0)
        {
            _dialogs.ShowError("Restore not possible",
                "None of the selected packages passed validation:\n\n" +
                string.Join("\n", invalid.Select(i => $"• {i.Entry.OriginalInf}: {i.Validation.Errors[0]}")));
            return;
        }

        var details = valid.Select(e => new KeyValuePair<string, string>(e.OriginalInf, $"{e.Provider} {e.Version}".Trim())).ToList();
        var warning = "Windows installs each driver only on matching devices and keeps the best-ranked driver, so an older version may not replace a newer one. A restart may be required.";
        if (invalid.Count > 0) warning = $"{invalid.Count} package(s) failed validation and will be skipped. " + warning;

        if (!_dialogs.Confirm(new ConfirmRequest("Restore drivers?",
                $"DRIVELY will install {valid.Count} validated driver package(s) from this backup using Windows PnPUtil.",
                "Restore", details, warning))) return;

        IsBusy = true;
        try
        {
            var progress = new Progress<string>(s => StatusText = s);
            var results = await _restore.RestoreAsync(backup, valid, progress);
            var succeeded = results.Count(r => r.Result.Success);
            var reboot = results.Any(r => r.Result.RebootRequired);
            StatusText = $"Restore finished: {succeeded} succeeded, {results.Count - succeeded} failed.";

            var summary = string.Join("\n", results.Select(r => $"• {r.Entry.OriginalInf}: {r.Result.Message}"));
            if (reboot) summary += "\n\nA restart is required to finish installation.";
            if (succeeded == results.Count) _dialogs.ShowInfo("Restore finished", summary);
            else _dialogs.ShowError("Restore finished with errors", summary);
        }
        catch (Exception ex)
        {
            _log.Error("Restore failed.", ex);
            _dialogs.ShowError("Restore failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanActOnBackup))]
    private void DeleteBackup()
    {
        if (SelectedBackup is not { } backup) return;
        if (!_dialogs.Confirm(new ConfirmRequest("Delete backup?",
                $"The backup folder \"{backup.Name}\" and all driver packages in it will be permanently deleted.",
                "Delete", IsDestructive: true))) return;
        try
        {
            _backups.DeleteBackup(backup);
            StatusText = $"Deleted {backup.Name}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.ShowError("Delete failed", ex.Message);
        }
        RefreshBackups();
    }

    [RelayCommand(CanExecute = nameof(CanActOnBackup))]
    private void OpenBackupFolder()
    {
        if (SelectedBackup is { } backup) _dialogs.OpenFolder(backup.FolderPath);
    }

    [RelayCommand]
    private void SelectAll(string? value)
    {
        var select = value != "none";
        foreach (var item in Entries) item.IsSelected = select && item.Entry.Exported;
    }

    private bool EnsureElevated(string action)
    {
        if (_elevation.IsElevated) return true;
        var restart = _dialogs.Confirm(new ConfirmRequest("Administrator permissions are required",
            $"{action} requires administrator permissions. DRIVELY will restart as administrator (Windows will ask for permission).",
            "Restart as administrator"));
        if (restart && !_elevation.TryRestartElevated())
            _dialogs.ShowInfo("Administrator permissions", "Administrator permissions were not granted. Nothing was changed.");
        return false;
    }
}
