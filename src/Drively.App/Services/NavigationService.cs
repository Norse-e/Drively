namespace Drively.App.Services;

public enum PageKey
{
    Dashboard,
    Drivers,
    Backup,
    History,
    Settings,
}

/// <summary>Lets view models request navigation without referencing the main window.</summary>
public sealed class NavigationService
{
    public event Action<PageKey, object?>? NavigationRequested;

    public void Navigate(PageKey page, object? parameter = null) => NavigationRequested?.Invoke(page, parameter);
}

/// <summary>Implemented by page view models that refresh when shown.</summary>
public interface INavigationAware
{
    void OnNavigatedTo(object? parameter);
}
