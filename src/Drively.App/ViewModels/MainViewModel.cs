using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Drively.App.Services;
using Drively.Core.Interfaces;

namespace Drively.App.ViewModels;

/// <summary>Shell: sidebar navigation, elevation status and startup.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly Dictionary<PageKey, object> _pages;
    private readonly IElevationService _elevation;
    private readonly DriverSession _session;
    private readonly ISettingsRepository _settings;
    private readonly DialogService _dialogs;

    [ObservableProperty]
    private object _currentPage;

    [ObservableProperty]
    private PageKey _currentPageKey = PageKey.Dashboard;

    public MainViewModel(
        DashboardViewModel dashboard,
        DriversViewModel drivers,
        BackupViewModel backup,
        HistoryViewModel history,
        SettingsViewModel settingsPage,
        NavigationService navigation,
        IElevationService elevation,
        DriverSession session,
        ISettingsRepository settings,
        DialogService dialogs,
        AppEnvironment environment)
    {
        _pages = new()
        {
            [PageKey.Dashboard] = dashboard,
            [PageKey.Drivers] = drivers,
            [PageKey.Backup] = backup,
            [PageKey.History] = history,
            [PageKey.Settings] = settingsPage,
        };
        _elevation = elevation;
        _session = session;
        _settings = settings;
        _dialogs = dialogs;
        Environment = environment;
        _currentPage = dashboard;
        navigation.NavigationRequested += (page, parameter) => NavigateTo(page, parameter);
    }

    public AppEnvironment Environment { get; }
    public bool IsElevated => _elevation.IsElevated;
    public string ElevationText => IsElevated ? "Running as administrator" : "Standard user";

    [RelayCommand]
    private void Navigate(PageKey page) => NavigateTo(page, null);

    private void NavigateTo(PageKey page, object? parameter)
    {
        CurrentPageKey = page;
        CurrentPage = _pages[page];
        if (CurrentPage is INavigationAware aware) aware.OnNavigatedTo(parameter);
    }

    [RelayCommand]
    private void RestartAsAdministrator()
    {
        if (!_elevation.TryRestartElevated())
            _dialogs.ShowInfo("Administrator permissions", "DRIVELY is still running as a standard user. Administrator permissions were not granted.");
    }

    /// <summary>Startup: read system info and run one local scan. Update checks only if the user enabled them.</summary>
    public async Task InitializeAsync()
    {
        await _session.LoadSystemInfoAsync();
        await _session.ScanAsync();
        if (_settings.Load().CheckForUpdatesOnStartup) await _session.CheckForUpdatesAsync();
    }
}
