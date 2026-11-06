namespace Drively.Core.Models;

/// <summary>Result of one hardware/driver scan. All counts are computed from detected devices.</summary>
public sealed class ScanResult
{
    public required IReadOnlyList<DeviceInfo> Devices { get; init; }
    public DateTimeOffset ScannedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>Non-fatal problems encountered while scanning (e.g. driver details unavailable).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public int TotalDevices => Devices.Count;
    public int InstalledDrivers => Devices.Count(d => d.HasDriver);
    public int ProblemDevices => Devices.Count(d => d.State == DeviceState.Error);
    public int MissingDrivers => Devices.Count(d => d.State == DeviceState.MissingDriver);
    public int DisabledDevices => Devices.Count(d => d.State == DeviceState.Disabled);
}
