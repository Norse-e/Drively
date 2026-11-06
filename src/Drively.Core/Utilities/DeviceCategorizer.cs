using Drively.Core.Models;

namespace Drively.Core.Utilities;

/// <summary>Maps Windows setup classes (PNPClass) to DRIVELY categories.</summary>
public static class DeviceCategorizer
{
    private static readonly Dictionary<string, DeviceCategory> ClassMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Display"] = DeviceCategory.Graphics,

        ["Media"] = DeviceCategory.Audio,
        ["AudioEndpoint"] = DeviceCategory.Audio,
        ["AudioProcessingObject"] = DeviceCategory.Audio,

        ["Net"] = DeviceCategory.Network,
        ["NetService"] = DeviceCategory.Network,
        ["NetTrans"] = DeviceCategory.Network,
        ["NetClient"] = DeviceCategory.Network,

        ["Bluetooth"] = DeviceCategory.Bluetooth,
        ["BthLEDevice"] = DeviceCategory.Bluetooth,

        ["System"] = DeviceCategory.Chipset,
        ["Processor"] = DeviceCategory.Chipset,
        ["Computer"] = DeviceCategory.Chipset,
        ["Firmware"] = DeviceCategory.Chipset,
        ["SecurityDevices"] = DeviceCategory.Chipset,

        ["HDC"] = DeviceCategory.Storage,
        ["SCSIAdapter"] = DeviceCategory.Storage,
        ["DiskDrive"] = DeviceCategory.Storage,
        ["CDROM"] = DeviceCategory.Storage,

        ["USB"] = DeviceCategory.Usb,
        ["USBDevice"] = DeviceCategory.Usb,

        ["Keyboard"] = DeviceCategory.Input,
        ["Mouse"] = DeviceCategory.Input,
        ["HIDClass"] = DeviceCategory.Input,

        ["Monitor"] = DeviceCategory.Monitors,
    };

    public static DeviceCategory Categorize(string? pnpClass, string? deviceName)
    {
        // Bluetooth adapters/services are often registered under other classes (e.g. Net for PAN).
        if (deviceName?.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) == true)
            return DeviceCategory.Bluetooth;

        return !string.IsNullOrWhiteSpace(pnpClass) && ClassMap.TryGetValue(pnpClass, out var category)
            ? category
            : DeviceCategory.Other;
    }

    /// <summary>Maps a Configuration Manager problem code to a device state.</summary>
    public static DeviceState StateFromProblemCode(int problemCode) => problemCode switch
    {
        0 => DeviceState.Working,
        22 => DeviceState.Disabled,
        28 => DeviceState.MissingDriver,
        45 => DeviceState.Disconnected,
        _ => DeviceState.Error,
    };
}
