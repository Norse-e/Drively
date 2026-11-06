using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Drively.App.Services;
using Drively.Core.Models;

namespace Drively.App.ViewModels;

/// <summary>One verified driver update with explicit Download / Download &amp; Install actions.</summary>
public sealed partial class UpdateItemViewModel(DriverUpdate update, DriversViewModel owner) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(DownloadAndInstallCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(DownloadAndInstallCommand))]
    private bool _isInstalled;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _statusIsError;

    public DriverUpdate Update { get; } = update;
    public string DownloadedText => Update.Candidate.IsDownloaded ? "Downloaded" : "";

    private bool CanAct() => !IsBusy && !IsInstalled && !owner.IsOperationRunning;

    public void RefreshCommands()
    {
        DownloadCommand.NotifyCanExecuteChanged();
        DownloadAndInstallCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task Download()
    {
        if (!owner.EnsureElevated()) return;
        await RunAsync("Downloading through Windows Update…", () => owner.Installer.DownloadAsync(Update), install: false);
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task DownloadAndInstall()
    {
        if (!owner.EnsureElevated()) return;

        var confirmed = owner.Dialogs.Confirm(new ConfirmRequest(
            "Install driver update?",
            $"DRIVELY will ask Windows Update to install this driver for \"{Update.DeviceName}\". Nothing is installed unless you confirm.",
            "Download & Install",
            [
                new("Device", Update.DeviceName),
                new("Current version", Update.CurrentVersion),
                new("New version", Update.NewVersion),
                new("Driver provider", Update.Provider),
                new("Download source", Update.Source),
                new("Signature", Update.SignatureStatus),
                new("Download size", Update.SizeText),
                new("Matched by", $"{(Update.MatchKind == MatchKind.HardwareId ? "Hardware ID" : "Compatible ID")} {Update.MatchedId}"),
            ],
            "The device may briefly stop working while the driver is replaced, and a restart may be required. " +
            "Consider creating a driver backup first (Backup & Restore)."));
        if (!confirmed) return;

        await RunAsync("Downloading and installing…", () => owner.Installer.DownloadAndInstallAsync(Update), install: true);
    }

    private async Task RunAsync(string progressText, Func<Task<OperationResult>> operation, bool install)
    {
        IsBusy = true;
        owner.IsOperationRunning = true;
        StatusIsError = false;
        StatusText = progressText;
        try
        {
            var result = await operation();
            StatusIsError = !result.Success;
            StatusText = result.Message;
            if (result.Success && install) IsInstalled = true;
            OnPropertyChanged(nameof(DownloadedText));

            if (install && result.Success && result.RebootRequired)
                owner.Dialogs.ShowInfo("A restart is required", "The driver was installed. Restart Windows to finish the installation. DRIVELY will not restart your PC automatically.");
            else if (!result.Success)
                owner.Dialogs.ShowError(install ? "Installation failed" : "Download failed", result.Message);
        }
        finally
        {
            IsBusy = false;
            owner.IsOperationRunning = false;
        }
    }
}
