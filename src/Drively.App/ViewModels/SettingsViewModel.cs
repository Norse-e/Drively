using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Drively.App.Services;
using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.Core.Utilities;

namespace Drively.App.ViewModels;

/// <summary>Essential settings only.</summary>
public sealed partial class SettingsViewModel : ObservableObject, INavigationAware
{
    private readonly ISettingsRepository _settings;
    private readonly ThemeService _theme;
    private readonly IHistoryRepository _history;
    private readonly DialogService _dialogs;
    private bool _loading;

    [ObservableProperty] private bool _isSystemTheme;
    [ObservableProperty] private bool _isLightTheme;
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private string _downloadDirectory = "";
    [ObservableProperty] private string _backupDirectory = "";
    [ObservableProperty] private bool _checkForUpdatesOnStartup;

    public SettingsViewModel(
        ISettingsRepository settings,
        ThemeService theme,
        IHistoryRepository history,
        DialogService dialogs,
        AppEnvironment environment,
        IEnumerable<IUpdateProvider> providers)
    {
        _settings = settings;
        _theme = theme;
        _history = history;
        _dialogs = dialogs;
        Version = environment.Version + (environment.IsDemoMode ? " (demo mode)" : "");
        NetworkSources = providers.Select(p => $"{p.Name}: {p.NetworkDisclosure}").ToList();
        Load();
    }

    public string Version { get; }
    public IReadOnlyList<string> NetworkSources { get; }
    public string DataLocation => AppPaths.DataRoot;

    public void OnNavigatedTo(object? parameter) => Load();

    private void Load()
    {
        _loading = true;
        var s = _settings.Load();
        IsSystemTheme = s.Theme == ThemePreference.System;
        IsLightTheme = s.Theme == ThemePreference.Light;
        IsDarkTheme = s.Theme == ThemePreference.Dark;
        DownloadDirectory = s.DownloadDirectory;
        BackupDirectory = s.BackupDirectory;
        CheckForUpdatesOnStartup = s.CheckForUpdatesOnStartup;
        _loading = false;
    }

    partial void OnIsSystemThemeChanged(bool value) { if (value) SetTheme(ThemePreference.System); }
    partial void OnIsLightThemeChanged(bool value) { if (value) SetTheme(ThemePreference.Light); }
    partial void OnIsDarkThemeChanged(bool value) { if (value) SetTheme(ThemePreference.Dark); }

    partial void OnCheckForUpdatesOnStartupChanged(bool value) => Update(s => s.CheckForUpdatesOnStartup = value);

    private void SetTheme(ThemePreference theme)
    {
        if (_loading) return;
        _theme.Apply(theme);
        Update(s => s.Theme = theme);
    }

    private void Update(Action<AppSettings> change)
    {
        if (_loading) return;
        var s = _settings.Load();
        change(s);
        _settings.Save(s);
    }

    [RelayCommand]
    private void ChangeDownloadDirectory()
    {
        var folder = _dialogs.PickFolder("Choose the download directory", DownloadDirectory);
        if (folder is null) return;
        DownloadDirectory = folder;
        Update(s => s.DownloadDirectory = folder);
    }

    [RelayCommand]
    private void ChangeBackupDirectory()
    {
        var folder = _dialogs.PickFolder("Choose the backup directory", BackupDirectory);
        if (folder is null) return;
        BackupDirectory = folder;
        Update(s => s.BackupDirectory = folder);
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        if (!_dialogs.Confirm(new ConfirmRequest("Clear history?",
                "All history entries stored by DRIVELY on this PC will be deleted.", "Clear history", IsDestructive: true))) return;
        await _history.ClearAsync();
        _dialogs.ShowInfo("History cleared", "The application history was cleared.");
    }

    [RelayCommand]
    private void OpenLogFolder() => _dialogs.OpenFolder(AppPaths.LogDirectory);

    [RelayCommand]
    private void OpenDataFolder() => _dialogs.OpenFolder(AppPaths.DataRoot);
}
