using Drively.Core.Models;
using Drively.Core.Utilities;
using Drively.DriverDetection;
using Drively.DriverDetection.Mock;

namespace Drively.DriverDetection.Tests;

public class DeviceIdentifierTests
{
    [Theory]
    [InlineData(@"PCI\VEN_10DE&DEV_2484&SUBSYS_146B10DE&REV_A1", "10DE", "2484")]
    [InlineData(@"HDAUDIO\FUNC_01&VEN_10EC&DEV_0887&SUBSYS_1458A0C3", "10EC", "0887")]
    [InlineData(@"USB\VID_046D&PID_C52B&REV_2410", "046D", "C52B")]
    [InlineData(@"ACPI\PNP0A08", null, null)]
    public void Parses_vendor_and_product_ids(string hwid, string? vendor, string? product)
    {
        var (v, p) = DeviceIdentifier.ParseVendorProduct(hwid);
        Assert.Equal(vendor, v);
        Assert.Equal(product, p);
    }

    [Fact]
    public void Builds_device_with_driver_details()
    {
        var entity = new PnpEntityRecord(@"PCI\VEN_10DE&DEV_2484\4&1", "NVIDIA GeForce RTX 3070", "NVIDIA", "Display", null, 0, "OK",
            [@"PCI\VEN_10DE&DEV_2484&SUBSYS_146B10DE&REV_A1", @"PCI\VEN_10DE&DEV_2484"], [@"PCI\CC_030000"]);
        var driver = new SignedDriverRecord(entity.InstanceId, "NVIDIA", "32.0.15.6094", new DateTime(2024, 9, 1), "oem42.inf", true, "Microsoft Windows Hardware Compatibility Publisher");

        var device = DeviceIdentifier.Build(entity, driver);

        Assert.Equal(DeviceCategory.Graphics, device.Category);
        Assert.Equal(DeviceState.Working, device.State);
        Assert.Equal("10DE", device.VendorId);
        Assert.Equal("NVIDIA", DeviceIdentifier.VendorName(device.VendorId));
        Assert.True(device.Driver!.IsThirdParty);
        Assert.Equal("32.0.15.6094", device.DriverVersionText);
    }

    [Fact]
    public void Device_without_driver_record_is_reported_as_missing()
    {
        var entity = new PnpEntityRecord(@"PCI\VEN_FFFF&DEV_0001\1", null, null, null, null, 28, "Error", [@"PCI\VEN_FFFF&DEV_0001"], []);
        var device = DeviceIdentifier.Build(entity, new SignedDriverRecord(entity.InstanceId, null, null, null, null, null, null));

        Assert.Equal("Unknown device", device.Name);
        Assert.Equal(DeviceState.MissingDriver, device.State);
        Assert.Null(device.Driver);
        Assert.True(device.HasProblem);
    }
}

public class MockHardwareProviderTests
{
    [Fact]
    public async Task Mock_provider_covers_all_states_for_ui_testing()
    {
        var scan = await new MockHardwareProvider().ScanAsync();

        Assert.Contains(scan.Devices, d => d.State == DeviceState.MissingDriver);
        Assert.Contains(scan.Devices, d => d.State == DeviceState.Error);
        Assert.Contains(scan.Devices, d => d.State == DeviceState.Disabled);
        Assert.All(scan.Devices, d => Assert.Contains("(demo)", d.Name)); // never confusable with real hardware
        Assert.All(scan.Devices, d => Assert.Equal("FFFF", d.VendorId));
    }

    [Fact]
    public void Mock_driver_store_lists_only_third_party_packages()
    {
        Assert.All(new MockDriverStoreReader().GetThirdPartyPackages(), p => Assert.StartsWith("oem", p.PublishedName));
    }
}

public class DriverStoreReaderTests
{
    [Fact]
    public void Reads_only_oem_infs_in_numeric_order()
    {
        var dir = Directory.CreateTempSubdirectory("drively-inf-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "oem10.inf"), "[Version]\nSignature=\"$Windows NT$\"\nClass=Net\nProvider=%P%\nDriverVer=01/02/2024,2.0.0.0\n[Strings]\nP=Contoso\n");
            File.WriteAllText(Path.Combine(dir, "oem2.inf"), "[Version]\nSignature=\"$Windows NT$\"\nClass=Display\nProvider=Fabrikam\nDriverVer=03/04/2023,1.0.0.0\n");
            File.WriteAllText(Path.Combine(dir, "usbport.inf"), "[Version]\nSignature=\"$Windows NT$\"\n");

            var packages = new DriverStoreReader(NullLog.Instance, dir).GetThirdPartyPackages();

            Assert.Equal(["oem2.inf", "oem10.inf"], packages.Select(p => p.PublishedName));
            Assert.Equal("Contoso", packages[1].Provider);
            Assert.Equal("2.0.0.0", packages[1].Version);
            Assert.Equal(new DateTime(2024, 1, 2), packages[1].Date);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
