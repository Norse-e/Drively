using Drively.Core.Models;
using Drively.Core.Utilities;
using Drively.DriverDetection;

namespace Drively.Integration.Tests;

/// <summary>Read-only tests against the real machine (WMI). They never modify drivers.</summary>
public class HardwareDetectionIntegrationTests
{
    [Fact]
    public async Task Real_scan_detects_devices_with_driver_information()
    {
        var scan = await new WmiHardwareProvider(NullLog.Instance).ScanAsync();

        Assert.NotEmpty(scan.Devices);
        Assert.All(scan.Devices, d => Assert.False(string.IsNullOrWhiteSpace(d.InstanceId)));
        Assert.Contains(scan.Devices, d => d.HasDriver && !string.IsNullOrEmpty(d.Driver!.Version) && !string.IsNullOrEmpty(d.Driver.InfName));
        Assert.Contains(scan.Devices, d => d.HardwareIds.Count > 0);
        Assert.True(scan.InstalledDrivers <= scan.TotalDevices);
    }

    [Fact]
    public async Task Real_system_info_is_read()
    {
        var info = await new WmiSystemInfoProvider(NullLog.Instance).GetSystemInfoAsync();
        Assert.StartsWith("Windows", info.WindowsVersion);
        Assert.False(string.IsNullOrWhiteSpace(info.WindowsBuild));
        Assert.Contains("64", info.Architecture);
    }

    [Fact]
    public void Real_driver_store_lists_third_party_packages()
    {
        var packages = new DriverStoreReader(NullLog.Instance).GetThirdPartyPackages();
        Assert.All(packages, p => Assert.Matches(@"^oem\d+\.inf$", p.PublishedName));
    }
}
