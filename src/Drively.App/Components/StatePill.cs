using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Drively.Core.Models;

namespace Drively.App.Components;

/// <summary>Colored pill showing a device's state. Binds to the DeviceInfo DataContext.</summary>
public sealed class StatePill : Border
{
    public StatePill()
    {
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(8, 1, 8, 2);
        HorizontalAlignment = HorizontalAlignment.Left;

        var text = new TextBlock { FontSize = 12 };
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(DeviceInfo.StateName)));
        text.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(DeviceInfo.State)) { Converter = StateBrushConverter.Foreground });
        SetBinding(BackgroundProperty, new Binding(nameof(DeviceInfo.State)) { Converter = StateBrushConverter.Background });
        Child = text;
    }
}

/// <summary>Maps a device state to theme brushes (resolved dynamically so theme switches apply on rescan).</summary>
public sealed class StateBrushConverter(bool background) : IValueConverter
{
    public static readonly StateBrushConverter Foreground = new(false);
    public static readonly StateBrushConverter Background = new(true);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            DeviceState.Working => "Success",
            DeviceState.Disabled or DeviceState.Disconnected => "Warning",
            DeviceState.MissingDriver => "Warning",
            DeviceState.Error => "Danger",
            _ => "Warning",
        };
        return Application.Current.TryFindResource("Brush." + key + (background ? "Soft" : ""));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Inverts a boolean (used to disable buttons while busy).</summary>
public sealed class InverseBool : IValueConverter
{
    public static readonly InverseBool Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
