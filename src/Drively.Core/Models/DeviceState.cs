namespace Drively.Core.Models;

/// <summary>Device health derived from the Windows Configuration Manager problem code.</summary>
public enum DeviceState
{
    Working,
    Disabled,
    MissingDriver,
    Error,
    Disconnected,
}

public static class DeviceStateExtensions
{
    public static string DisplayName(this DeviceState state) => state switch
    {
        DeviceState.Working => "Working",
        DeviceState.Disabled => "Disabled",
        DeviceState.MissingDriver => "Driver missing",
        DeviceState.Error => "Problem",
        DeviceState.Disconnected => "Not connected",
        _ => "Unknown",
    };
}
