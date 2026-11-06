using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Drively.App.Services;
using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.App.ViewModels;

/// <summary>History page: local record of downloads, installs, backups and restores.</summary>
public sealed partial class HistoryViewModel(IHistoryRepository history, DialogService dialogs, ILog log)
    : ObservableObject, INavigationAware
{
    [ObservableProperty]
    private string _statusText = "";

    public ObservableCollection<HistoryEntry> Entries { get; } = [];
    public bool IsEmpty => Entries.Count == 0;

    public void OnNavigatedTo(object? parameter) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var entries = await history.GetAllAsync();
            Entries.Clear();
            foreach (var entry in entries) Entries.Add(entry);
            StatusText = "";
        }
        catch (Exception ex)
        {
            log.Error("History could not be loaded.", ex);
            StatusText = "History could not be loaded.";
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (!dialogs.Confirm(new ConfirmRequest("Clear history?",
                "All history entries stored by DRIVELY on this PC will be deleted. Installed drivers and backups are not affected.",
                "Clear history", IsDestructive: true))) return;
        await history.ClearAsync();
        await LoadAsync();
    }
}
