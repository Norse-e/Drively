using Drively.Core.Utilities;

namespace Drively.Core.Models;

/// <summary>A Plug and Play device detected on this PC, with its driver (if any).</summary>
public sealed class DeviceInfo
{
    /// <summary>PnP device instance ID, e.g. <c>PCI\VEN_8086&amp;DEV_A0F0&amp;...\3&amp;11583659&amp;0&amp;A3</c>.</summary>
    public required string InstanceId { get; init; }

    public required string Name { get; init; }
    public string Manufacturer { get; init; } = "";

    /// <summary>Windows setup class name (PNPClass), e.g. Display, Net, MEDIA.</summary>
    public string DeviceClass { get; init; } = "";

    public string? ClassGuid { get; init; }
    public IReadOnlyList<string> HardwareIds { get; init; } = [];
    public IReadOnlyList<string> CompatibleIds { get; init; } = [];

    /// <summary>Configuration Manager problem code (0 = no problem).</summary>
    public int ProblemCode { get; init; }

    public DeviceCategory Category { get; init; } = DeviceCategory.Other;
    public DeviceState State { get; init; } = DeviceState.Working;
    public DriverDetails? Driver { get; init; }

    /// <summary>PCI/USB vendor ID parsed from the hardware ID, when present (e.g. 8086).</summary>
    public string? VendorId { get; init; }

    /// <summary>PCI/USB device/product ID parsed from the hardware ID, when present.</summary>
    public string? ProductId { get; init; }

    public string PrimaryHardwareId => HardwareIds.Count > 0 ? HardwareIds[0] : InstanceId;
    public bool HasDriver => Driver is not null;
    public bool HasProblem => State is DeviceState.Error or DeviceState.MissingDriver;

    public string CategoryName => Category.DisplayName();
    public string StateName => State.DisplayName();
    public string ProblemDescription => DeviceProblemCodes.Describe(ProblemCode);
    public string DriverVersionText => Driver?.Version is { Length: > 0 } v ? v : "—";
    public string DriverProviderText => Driver?.Provider is { Length: > 0 } p ? p : "—";
    public string DriverDateText => Driver?.Date is { } d ? d.ToString("yyyy-MM-dd") : "—";
}
