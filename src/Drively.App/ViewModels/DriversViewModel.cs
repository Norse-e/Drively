using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Drively.App.Services;
using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.Core.Services;
using Drively.Core.Utilities;
using Drively.DriverInstallation;
using Drively.DriverUpdates;

namespace Drively.App.ViewModels;

public sealed record CategoryFilter(string Label, int Count, Func<DeviceInfo, bool> Predicate)
{
    public string CountText => Count.ToString();
}

/// <summary>Drivers page: installed driver inventory (with filters and details) and verified updates.</summary>
public sealed partial class DriversViewModel : ObservableObject, INavigationAware
{
    public const string UpdatesTab = "updates";
    public const string ProblemsFilter = "problems";

    private readonly ManufacturerProvider _manufacturers;
    private readonly InstallationManager _installer;
    private readonly IElevationService _elevation;
    private readonly ISettingsRepository _settings;
    private readonly ILog _log;

    [ObservableProperty]
    private CategoryFilter? _selectedCategory;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isOperationRunning;

    public DriversViewModel(
        DriverSession session,
        ManufacturerProvider manufacturers,
        InstallationManager installer,
        IElevationService elevation,
        ISettingsRepository settings,
        DialogService dialogs,
        ILog log)
    {
        Session = session;
        _manufacturers = manufacturers;
        _installer = installer;
        _elevation = elevation;
        _settings = settings;
        Dialogs = dialogs;
        _log = log;
        session.PropertyChanged += OnSessionChanged;
        RebuildCategories();
        RebuildUpdates();
    }

    public DriverSession Session { get; }
    internal DialogService Dialogs { get; }
    public bool IsElevated => _elevation.IsElevated;

    public ObservableCollection<CategoryFilter> Categories { get; } = [];
    public ObservableCollection<DeviceInfo> FilteredDevices { get; } = [];
    public ObservableCollection<SupportLink> SelectedDeviceLinks { get; } = [];
    public ObservableCollection<UpdateItemViewModel> Updates { get; } = [];

    public bool HasSelection => SelectedDevice is not null;
    public string UpdatesHeader => Session.Updates is { Succeeded: true } u ? $"Available updates ({u.Updates.Count})" : "Available updates";

    public string UpdatesMessage => Session.Updates switch
    {
        null when Session.IsCheckingUpdates => "Checking Windows Update for driver updates. This can take a few minutes…",
        null => "Updates have not been checked yet. DRIVELY contacts Windows Update only when you click \"Check for updates\".",
        var u => u.Message,
    };

    public bool UpdateCheckFailed => Session.Updates is { Succeeded: false };

    public string RejectedText => Session.Updates is { Succeeded: true, RejectedCount: > 0 } u
        ? $"{u.RejectedCount} offered update{(u.RejectedCount == 1 ? " was" : "s were")} not shown because compatibility could not be verified or the installed driver is the same or newer. Details are in the log."
        : "";

    public void OnNavigatedTo(object? parameter)
    {
        if (parameter is UpdatesTab) SelectedTabIndex = 1;
        else if (parameter is ProblemsFilter)
        {
            SelectedTabIndex = 0;
            SelectedCategory = Categories.FirstOrDefault(c => c.Label == "Problems");
        }
    }

    partial void OnSelectedCategoryChanged(CategoryFilter? value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedDeviceChanged(DeviceInfo? value)
    {
        SelectedDeviceLinks.Clear();
        if (value is null) return;
        foreach (var link in _manufacturers.GetSupportLinks(value, Session.System)) SelectedDeviceLinks.Add(link);
    }

    partial void OnIsOperationRunningChanged(bool value)
    {
        foreach (var item in Updates) item.RefreshCommands();
    }

    [RelayCommand]
    private Task Scan() => Session.ScanAsync();

    [RelayCommand]
    private Task CheckForUpdates() => Session.CheckForUpdatesAsync();

    [RelayCommand]
    private void OpenLink(SupportLink? link)
    {
        if (link is not null) Dialogs.OpenUri(link.Url);
    }

    [RelayCommand]
    private void ExportCsv() => Export("csv");

    [RelayCommand]
    private void ExportJson() => Export("json");

    private void Export(string format)
    {
        if (Session.Scan is null) return;
        var path = Dialogs.PickSaveFile(
            "Export driver inventory",
            $"drively-inventory-{DateTime.Now:yyyyMMdd-HHmm}.{format}",
            format == "csv" ? "CSV file (*.csv)|*.csv" : "JSON file (*.json)|*.json",
            _settings.Load().DownloadDirectory);
        if (path is null) return;

        try
        {
            var content = format == "csv"
                ? InventoryExporter.ToCsv(Session.Scan.Devices)
                : InventoryExporter.ToJson(Session.Scan.Devices, Session.System, Session.Scan.ScannedAt);
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
            Dialogs.ShowInfo("Inventory exported", $"The driver inventory was saved to:\n{path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogs.ShowError("Export failed", ex.Message);
        }
    }

    internal bool EnsureElevated()
    {
        if (_elevation.IsElevated) return true;
        var restart = Dialogs.Confirm(new ConfirmRequest(
            "Administrator permissions are required",
            "Downloading and installing drivers through Windows Update requires administrator permissions. " +
            "DRIVELY will restart as administrator (Windows will ask for permission). Scan results are refreshed after the restart.",
            "Restart as administrator"));
        if (restart && !_elevation.TryRestartElevated())
            Dialogs.ShowInfo("Administrator permissions", "Administrator permissions were not granted. Nothing was changed.");
        return false;
    }

    internal InstallationManager Installer => _installer;
    internal ILog Log => _log;

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DriverSession.Scan):
                RebuildCategories();
                break;
            case nameof(DriverSession.Updates):
                RebuildUpdates();
                break;
        }

        OnPropertyChanged(nameof(UpdatesHeader));
        OnPropertyChanged(nameof(UpdatesMessage));
        OnPropertyChanged(nameof(UpdateCheckFailed));
        OnPropertyChanged(nameof(RejectedText));
    }

    private void RebuildCategories()
    {
        var devices = Session.Scan?.Devices ?? [];
        var previous = SelectedCategory?.Label;

        Categories.Clear();
        Categories.Add(new CategoryFilter("All devices", devices.Count, _ => true));
        Categories.Add(new CategoryFilter("Problems", devices.Count(d => d.HasProblem || d.State == DeviceState.Disabled),
            d => d.HasProblem || d.State == DeviceState.Disabled));
        foreach (var category in Enum.GetValues<DeviceCategory>())
        {
            var count = devices.Count(d => d.Category == category);
            if (count > 0) Categories.Add(new CategoryFilter(category.DisplayName(), count, d => d.Category == category));
        }

        SelectedCategory = Categories.FirstOrDefault(c => c.Label == previous) ?? Categories[0];
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var devices = Session.Scan?.Devices ?? [];
        var predicate = SelectedCategory?.Predicate ?? (_ => true);
        var search = SearchText.Trim();

        var selectedId = SelectedDevice?.InstanceId;
        FilteredDevices.Clear();
        foreach (var device in devices.Where(predicate))
        {
            if (search.Length > 0 &&
                !device.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) &&
                !device.DriverProviderText.Contains(search, StringComparison.CurrentCultureIgnoreCase) &&
                !device.PrimaryHardwareId.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            FilteredDevices.Add(device);
        }
        SelectedDevice = FilteredDevices.FirstOrDefault(d => d.InstanceId == selectedId);
    }

    private void RebuildUpdates()
    {
        Updates.Clear();
        if (Session.Updates is not { Succeeded: true } result) return;
        foreach (var update in result.Updates) Updates.Add(new UpdateItemViewModel(update, this));
    }
}
