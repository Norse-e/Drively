namespace Drively.Core.Models;

/// <summary>Basic PC information shown on the dashboard. Stays on this PC.</summary>
public sealed record SystemInfo
{
    public string WindowsVersion { get; init; } = "Unknown";
    public string WindowsBuild { get; init; } = "";
    public string Architecture { get; init; } = "Unknown";
    public string Manufacturer { get; init; } = "Unknown";
    public string Model { get; init; } = "Unknown";
    public string BaseBoardManufacturer { get; init; } = "";
    public string BaseBoardProduct { get; init; } = "";
}
