namespace Drively.Core.Models;

/// <summary>User-facing device categories shown in the driver inventory.</summary>
public enum DeviceCategory
{
    Graphics,
    Audio,
    Network,
    Bluetooth,
    Chipset,
    Storage,
    Usb,
    Input,
    Monitors,
    Other,
}

public static class DeviceCategoryExtensions
{
    public static string DisplayName(this DeviceCategory category) => category switch
    {
        DeviceCategory.Graphics => "Graphics adapters",
        DeviceCategory.Audio => "Audio devices",
        DeviceCategory.Network => "Network adapters",
        DeviceCategory.Bluetooth => "Bluetooth",
        DeviceCategory.Chipset => "Chipset & system",
        DeviceCategory.Storage => "Storage controllers",
        DeviceCategory.Usb => "USB controllers",
        DeviceCategory.Input => "Input devices",
        DeviceCategory.Monitors => "Monitors",
        _ => "Other devices",
    };
}
