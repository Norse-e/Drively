namespace Drively.Core.Models;

/// <summary>Information about the driver currently bound to a device.</summary>
public sealed record DriverDetails
{
    public string Provider { get; init; } = "";
    public string Version { get; init; } = "";
    public DateTime? Date { get; init; }

    /// <summary>Published INF name in the driver store, e.g. <c>oem42.inf</c> or <c>usbport.inf</c>.</summary>
    public string InfName { get; init; } = "";

    public bool? IsSigned { get; init; }
    public string? Signer { get; init; }

    /// <summary>Third-party packages are published as oemNN.inf; inbox Windows drivers are not.</summary>
    public bool IsThirdParty => InfName.StartsWith("oem", StringComparison.OrdinalIgnoreCase);
}
