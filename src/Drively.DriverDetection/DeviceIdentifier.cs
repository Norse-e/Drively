using System.Text.RegularExpressions;
using Drively.Core.Models;
using Drively.Core.Utilities;

namespace Drively.DriverDetection;

/// <summary>Builds <see cref="DeviceInfo"/> from raw WMI records and identifies vendors from hardware IDs.</summary>
public static partial class DeviceIdentifier
{
    /// <summary>A small table of well-known PCI-SIG / USB-IF vendor IDs, used only for display and support links.</summary>
    private static readonly Dictionary<string, string> KnownVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["8086"] = "Intel",
        ["8087"] = "Intel",
        ["10DE"] = "NVIDIA",
        ["1002"] = "AMD",
        ["1022"] = "AMD",
        ["10EC"] = "Realtek",
        ["0BDA"] = "Realtek",
        ["14E4"] = "Broadcom",
        ["168C"] = "Qualcomm Atheros",
        ["17CB"] = "Qualcomm",
        ["1B21"] = "ASMedia",
        ["144D"] = "Samsung",
        ["1179"] = "Toshiba",
        ["1E0F"] = "KIOXIA",
        ["15B7"] = "Western Digital",
        ["1C5C"] = "SK hynix",
        ["1987"] = "Phison",
        ["0B05"] = "ASUS",
        ["1043"] = "ASUS",
        ["1462"] = "MSI",
        ["1458"] = "Gigabyte",
        ["17AA"] = "Lenovo",
        ["1028"] = "Dell",
        ["103C"] = "HP",
        ["1025"] = "Acer",
        ["045E"] = "Microsoft",
        ["046D"] = "Logitech",
    };

    public static DeviceInfo Build(PnpEntityRecord entity, SignedDriverRecord? driver)
    {
        var name = FirstNonEmpty(entity.Name, "Unknown device");
        var (vendor, product) = ParseVendorProduct(entity.HardwareIds.Count > 0 ? entity.HardwareIds[0] : entity.InstanceId);

        DriverDetails? details = null;
        if (driver is not null && DriverScanner.HasDriver(driver))
        {
            details = new DriverDetails
            {
                Provider = driver.Provider ?? "",
                Version = driver.Version ?? "",
                Date = driver.Date,
                InfName = driver.InfName ?? "",
                IsSigned = driver.IsSigned,
                Signer = driver.Signer,
            };
        }

        return new DeviceInfo
        {
            InstanceId = entity.InstanceId,
            Name = name,
            Manufacturer = entity.Manufacturer ?? "",
            DeviceClass = entity.PnpClass ?? "",
            ClassGuid = entity.ClassGuid,
            HardwareIds = entity.HardwareIds,
            CompatibleIds = entity.CompatibleIds,
            ProblemCode = entity.ProblemCode,
            State = DeviceCategorizer.StateFromProblemCode(entity.ProblemCode),
            Category = DeviceCategorizer.Categorize(entity.PnpClass, name),
            Driver = details,
            VendorId = vendor,
            ProductId = product,
        };
    }

    /// <summary>Parses VEN_xxxx/DEV_xxxx (PCI, HDAUDIO) or VID_xxxx/PID_xxxx (USB) from a hardware ID.</summary>
    public static (string? Vendor, string? Product) ParseVendorProduct(string? hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId)) return (null, null);

        var pci = PciRegex().Match(hardwareId);
        if (pci.Success) return (pci.Groups["v"].Value.ToUpperInvariant(), pci.Groups["d"].Success ? pci.Groups["d"].Value.ToUpperInvariant() : null);

        var usb = UsbRegex().Match(hardwareId);
        if (usb.Success) return (usb.Groups["v"].Value.ToUpperInvariant(), usb.Groups["p"].Success ? usb.Groups["p"].Value.ToUpperInvariant() : null);

        return (null, null);
    }

    public static string? VendorName(string? vendorId) =>
        vendorId is not null && KnownVendors.TryGetValue(vendorId, out var name) ? name : null;

    private static string FirstNonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    [GeneratedRegex(@"VEN_(?<v>[0-9A-F]{4})(?:&DEV_(?<d>[0-9A-F]{4}))?", RegexOptions.IgnoreCase)]
    private static partial Regex PciRegex();

    [GeneratedRegex(@"VID_(?<v>[0-9A-F]{4})(?:&PID_(?<p>[0-9A-F]{4}))?", RegexOptions.IgnoreCase)]
    private static partial Regex UsbRegex();
}
