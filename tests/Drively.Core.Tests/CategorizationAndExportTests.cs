using Drively.Core.Models;
using Drively.Core.Services;
using Drively.Core.Utilities;

namespace Drively.Core.Tests;

public class CategorizationAndExportTests
{
    [Theory]
    [InlineData("Display", "GPU", DeviceCategory.Graphics)]
    [InlineData("MEDIA", "Realtek(R) Audio", DeviceCategory.Audio)]
    [InlineData("Net", "Intel(R) Ethernet", DeviceCategory.Network)]
    [InlineData("Net", "Bluetooth Device (Personal Area Network)", DeviceCategory.Bluetooth)]
    [InlineData("System", "PCI Express Root Port", DeviceCategory.Chipset)]
    [InlineData("SCSIAdapter", "Standard NVM Express Controller", DeviceCategory.Storage)]
    [InlineData("USB", "USB xHCI Compliant Host Controller", DeviceCategory.Usb)]
    [InlineData("HIDClass", "HID-compliant mouse", DeviceCategory.Input)]
    [InlineData("Monitor", "Generic PnP Monitor", DeviceCategory.Monitors)]
    [InlineData("SoftwareDevice", "Something", DeviceCategory.Other)]
    [InlineData(null, "Unknown device", DeviceCategory.Other)]
    public void Categorizes_by_setup_class(string? pnpClass, string name, DeviceCategory expected)
    {
        Assert.Equal(expected, DeviceCategorizer.Categorize(pnpClass, name));
    }

    [Theory]
    [InlineData(0, DeviceState.Working)]
    [InlineData(22, DeviceState.Disabled)]
    [InlineData(28, DeviceState.MissingDriver)]
    [InlineData(45, DeviceState.Disconnected)]
    [InlineData(10, DeviceState.Error)]
    [InlineData(43, DeviceState.Error)]
    public void Maps_problem_codes_to_states(int code, DeviceState expected)
    {
        Assert.Equal(expected, DeviceCategorizer.StateFromProblemCode(code));
    }

    [Fact]
    public void Problem_code_descriptions_are_human_readable()
    {
        Assert.Equal("The drivers for this device are not installed.", DeviceProblemCodes.Describe(28));
        Assert.Contains("999", DeviceProblemCodes.Describe(999));
    }

    [Fact]
    public void Scan_counts_are_computed_from_devices()
    {
        var scan = new ScanResult
        {
            Devices =
            [
                new DeviceInfo { InstanceId = "1", Name = "a", Driver = new DriverDetails() },
                new DeviceInfo { InstanceId = "2", Name = "b", State = DeviceState.MissingDriver, ProblemCode = 28 },
                new DeviceInfo { InstanceId = "3", Name = "c", State = DeviceState.Error, ProblemCode = 10, Driver = new DriverDetails() },
            ],
        };
        Assert.Equal(3, scan.TotalDevices);
        Assert.Equal(2, scan.InstalledDrivers);
        Assert.Equal(1, scan.MissingDrivers);
        Assert.Equal(1, scan.ProblemDevices);
    }

    [Fact]
    public void Csv_export_escapes_commas_quotes_and_formulas()
    {
        var csv = InventoryExporter.ToCsv(
        [
            new DeviceInfo { InstanceId = "1", Name = "Device, \"quoted\"", Manufacturer = "=HYPERLINK(\"x\")" },
        ]);
        var line = csv.Split(Environment.NewLine)[1];
        Assert.Contains("\"Device, \"\"quoted\"\"\"", line);
        Assert.Contains("'=HYPERLINK", line);
    }

    [Fact]
    public void Json_export_contains_devices()
    {
        var json = InventoryExporter.ToJson([new DeviceInfo { InstanceId = "1", Name = "Dev" }], null, DateTimeOffset.Now);
        Assert.Contains("\"name\": \"Dev\"", json);
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(60161, "58.8 KB")]
    [InlineData(32248281, "30.8 MB")]
    public void Formats_sizes(long bytes, string expected) => Assert.Equal(expected, ByteSize.Format(bytes));
}
