using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Drively.Core.Models;
using Microsoft.Win32;

namespace Drively.App.Services;

/// <summary>Applies light/dark/system theme by swapping the color dictionary, and themes window title bars.</summary>
public sealed class ThemeService
{
    private ThemePreference _preference = ThemePreference.System;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _preference == ThemePreference.System)
                Application.Current?.Dispatcher.Invoke(() => Apply(ThemePreference.System));
        };
    }

    public bool IsDark { get; private set; }

    public void Apply(ThemePreference preference)
    {
        _preference = preference;
        IsDark = preference switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            _ => SystemUsesDarkTheme(),
        };

        var source = new Uri(IsDark ? "/Drively;component/Resources/Theme.Dark.xaml" : "/Drively;component/Resources/Theme.Light.xaml", UriKind.Relative);
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary { Source = source };

        foreach (Window window in Application.Current.Windows) ApplyTitleBar(window);
    }

    public void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var value = IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    private static bool SystemUsesDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
