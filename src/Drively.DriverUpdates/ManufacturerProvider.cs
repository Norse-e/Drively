using Drively.Core.Models;

namespace Drively.DriverUpdates;

/// <summary>
/// Official manufacturer support pages. Hardware and PC manufacturers do not offer a public,
/// supported API for automated driver update discovery, so DRIVELY links to their official
/// support/download pages instead of scraping or using unofficial driver databases.
/// Links open in the user's browser only when clicked.
/// </summary>
public sealed class ManufacturerProvider
{
    private sealed record Vendor(string Name, string[] Keywords, string[] VendorIds, string Url);

    private static readonly Vendor[] Vendors =
    [
        new("NVIDIA", ["nvidia"], ["10DE"], "https://www.nvidia.com/en-us/drivers/"),
        new("AMD", ["advanced micro devices", "amd"], ["1002", "1022"], "https://www.amd.com/en/support/download/drivers.html"),
        new("Intel", ["intel"], ["8086", "8087"], "https://www.intel.com/content/www/us/en/support/detect.html"),
        new("Realtek", ["realtek"], ["10EC", "0BDA"], "https://www.realtek.com/"),
        new("ASUS", ["asus", "asustek"], ["1043", "0B05"], "https://www.asus.com/support/download-center/"),
        new("MSI", ["micro-star", "msi"], ["1462"], "https://www.msi.com/support/download"),
        new("Gigabyte", ["gigabyte"], ["1458"], "https://www.gigabyte.com/Support"),
        new("Lenovo", ["lenovo"], ["17AA"], "https://pcsupport.lenovo.com/"),
        new("Dell", ["dell"], ["1028"], "https://www.dell.com/support/home/"),
        new("HP", ["hewlett-packard", "hp inc", "hp"], ["103C"], "https://support.hp.com/drivers"),
        new("Acer", ["acer"], ["1025"], "https://www.acer.com/support"),
    ];

    public const string WindowsUpdateSettingsUri = "ms-settings:windowsupdate";

    /// <summary>Support links for a device: its chip vendor, plus the PC/motherboard maker.</summary>
    public IReadOnlyList<SupportLink> GetSupportLinks(DeviceInfo device, SystemInfo? system)
    {
        var links = new List<SupportLink>();

        var chipVendor = FindVendor(device.VendorId, device.Manufacturer, device.Driver?.Provider);
        if (chipVendor is not null)
        {
            var description = chipVendor.Name == "Realtek"
                ? "Official Realtek website. Realtek generally advises getting drivers for its chips from your PC or motherboard manufacturer."
                : $"Official {chipVendor.Name} support page. Your PC manufacturer may provide customized drivers for this device.";
            links.Add(new SupportLink($"{chipVendor.Name} driver support", chipVendor.Url, description));
        }

        foreach (var oem in new[] { system?.Manufacturer, system?.BaseBoardManufacturer })
        {
            var vendor = FindVendor(null, oem, null);
            if (vendor is not null && links.All(l => l.Url != vendor.Url))
            {
                links.Add(new SupportLink($"{vendor.Name} support for your PC", vendor.Url,
                    $"Drivers tailored to your {vendor.Name} PC or motherboard model."));
            }
        }

        links.Add(new SupportLink("Windows Update", WindowsUpdateSettingsUri,
            "Open Windows Update settings (Advanced options > Optional updates lists driver updates)."));
        return links;
    }

    private static Vendor? FindVendor(string? vendorId, string? manufacturer, string? provider)
    {
        if (!string.IsNullOrEmpty(vendorId))
        {
            var byId = Vendors.FirstOrDefault(v => v.VendorIds.Contains(vendorId, StringComparer.OrdinalIgnoreCase));
            if (byId is not null) return byId;
        }

        foreach (var text in new[] { manufacturer, provider })
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            var lowered = text.ToLowerInvariant();
            var match = Vendors.FirstOrDefault(v => v.Keywords.Any(k => ContainsWord(lowered, k)));
            if (match is not null) return match;
        }
        return null;
    }

    private static bool ContainsWord(string text, string keyword)
    {
        var index = text.IndexOf(keyword, StringComparison.Ordinal);
        while (index >= 0)
        {
            var startOk = index == 0 || !char.IsLetter(text[index - 1]);
            var end = index + keyword.Length;
            var endOk = end == text.Length || !char.IsLetter(text[end]);
            if (startOk && endOk) return true;
            index = text.IndexOf(keyword, index + 1, StringComparison.Ordinal);
        }
        return false;
    }
}
