using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Drively.App.Services;

namespace Drively.App.ViewModels;

/// <summary>Dashboard: system info, real counts from the latest scan, and main actions.</summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly NavigationService _navigation;

    public DashboardViewModel(DriverSession session, NavigationService navigation)
    {
        Session = session;
        _navigation = navigation;
        session.PropertyChanged += OnSessionChanged;
    }

    public DriverSession Session { get; }

    public string LastScanText => Session.LastScan is { } d ? d.LocalDateTime.ToString("yyyy-MM-dd HH:mm") : "Never";

    public string TotalDevices => Session.Scan?.TotalDevices.ToString() ?? "—";
    public string InstalledDrivers => Session.Scan?.InstalledDrivers.ToString() ?? "—";
    public string ProblemDevices => Session.Scan?.ProblemDevices.ToString() ?? "—";
    public string MissingDrivers => Session.Scan?.MissingDrivers.ToString() ?? "—";

    public string UpdatesAvailable => Session.Updates switch
    {
        null => "—",
        { Succeeded: false } => "!",
        var u => u.Updates.Count.ToString(),
    };

    public string UpdatesCaption => Session.Updates switch
    {
        null => "Not checked yet",
        { Succeeded: false } => "Check could not be completed",
        var u => "Checked " + u.CheckedAt.LocalDateTime.ToString("HH:mm"),
    };

    public string UpdateMessage => Session.Updates?.Message ?? "";
    public bool UpdateCheckFailed => Session.Updates is { Succeeded: false };

    [RelayCommand]
    private Task Scan() => Session.ScanAsync();

    [RelayCommand]
    private Task CheckForUpdates() => Session.CheckForUpdatesAsync();

    [RelayCommand]
    private void ViewUpdates() => _navigation.Navigate(PageKey.Drivers, DriversViewModel.UpdatesTab);

    [RelayCommand]
    private void ViewProblems() => _navigation.Navigate(PageKey.Drivers, DriversViewModel.ProblemsFilter);

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Simple and cheap: refresh all computed dashboard values.
        OnPropertyChanged(string.Empty);
    }
}
