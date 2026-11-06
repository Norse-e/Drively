using CommunityToolkit.Mvvm.ComponentModel;
using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.DriverDetection;
using Drively.DriverUpdates;

namespace Drively.App.Services;

/// <summary>
/// Shared, observable state for the current session: system info, the latest scan and the latest
/// update check. Scans run only when requested (and once at startup); nothing runs in the background.
/// </summary>
public sealed partial class DriverSession(
    IHardwareProvider hardware,
    ISystemInfoProvider systemInfo,
    UpdateChecker updateChecker,
    ISettingsRepository settingsRepository,
    ILog log) : ObservableObject
{
    [ObservableProperty]
    private SystemInfo? _system;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScan))]
    private ScanResult? _scan;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateResult))]
    private UpdateCheckResult? _updates;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string? _scanError;

    [ObservableProperty]
    private DateTimeOffset? _lastScan = settingsRepository.Load().LastScan;

    public bool IsBusy => IsScanning || IsCheckingUpdates;
    public bool HasScan => Scan is not null;
    public bool HasUpdateResult => Updates is not null;

    public async Task LoadSystemInfoAsync()
    {
        try
        {
            System = await systemInfo.GetSystemInfoAsync();
        }
        catch (Exception ex)
        {
            log.Error("System information could not be read.", ex);
            System = new SystemInfo();
        }
    }

    public async Task ScanAsync()
    {
        if (IsBusy) return;
        IsScanning = true;
        ScanError = null;
        StatusMessage = "Scanning hardware and drivers…";
        try
        {
            Scan = await hardware.ScanAsync();
            Updates = null; // Previous update results refer to the previous scan.
            LastScan = Scan.ScannedAt;
            SaveSetting(s => s.LastScan = Scan.ScannedAt);
            StatusMessage = $"Scan complete: {Scan.TotalDevices} devices detected.";
        }
        catch (DetectionException ex)
        {
            ScanError = ex.Message;
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            log.Error("Unexpected scan failure.", ex);
            ScanError = "Unable to detect devices. See the log for details.";
            StatusMessage = ScanError;
        }
        finally
        {
            IsScanning = false;
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        if (IsBusy) return;
        if (Scan is null)
        {
            await ScanAsync();
            if (Scan is null) return;
        }

        IsCheckingUpdates = true;
        StatusMessage = "Checking Windows Update for driver updates. This can take a few minutes…";
        try
        {
            Updates = await updateChecker.CheckAsync(Scan.Devices);
            StatusMessage = Updates.Message;
            if (Updates.Succeeded) SaveSetting(s => s.LastUpdateCheck = Updates.CheckedAt);
        }
        catch (Exception ex)
        {
            log.Error("Unexpected update check failure.", ex);
            Updates = new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Message = "Update discovery could not be completed. See the log for details." };
            StatusMessage = Updates.Message;
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    private void SaveSetting(Action<AppSettings> change)
    {
        try
        {
            var settings = settingsRepository.Load();
            change(settings);
            settingsRepository.Save(settings);
        }
        catch (Exception ex)
        {
            log.Warn("Could not save settings.", ex);
        }
    }
}
