using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.Core.Utilities;

namespace Drively.DriverDetection.Mock;

/// <summary>
/// Simulated hardware for UI development and tests (run DRIVELY with --demo).
/// Uses the fictional vendor "Contoso" and vendor ID FFFF so it can never be confused with real hardware.
/// </summary>
public sealed class MockHardwareProvider : IHardwareProvider
{
    public static IReadOnlyList<DeviceInfo> CreateDevices() =>
    [
        Device("Contoso Graphics 1000 (demo)", "Display", @"PCI\VEN_FFFF&DEV_0001", 0, "Contoso", "31.0.15.1000", new(2024, 3, 1), "oem10.inf"),
        Device("Contoso HD Audio (demo)", "MEDIA", @"HDAUDIO\FUNC_01&VEN_FFFF&DEV_0002", 0, "Contoso", "6.0.9500.1", new(2023, 11, 20), "oem11.inf"),
        Device("Contoso Ethernet Controller (demo)", "Net", @"PCI\VEN_FFFF&DEV_0003", 0, "Contoso", "10.68.815.2024", new(2024, 8, 15), "oem12.inf"),
        Device("Contoso Wi-Fi 7 Adapter (demo)", "Net", @"PCI\VEN_FFFF&DEV_0004", 0, "Contoso", "23.40.0.4", new(2024, 6, 2), "oem13.inf"),
        Device("Contoso Bluetooth Radio (demo)", "Bluetooth", @"USB\VID_FFFF&PID_0005", 0, "Contoso", "23.40.0.2", new(2024, 6, 2), "oem14.inf"),
        Device("Contoso SMBus Controller (demo)", "System", @"PCI\VEN_FFFF&DEV_0006", 0, "Contoso", "10.1.20.1", new(2022, 1, 5), "oem15.inf"),
        Device("Standard NVM Express Controller (demo)", "SCSIAdapter", @"PCI\VEN_FFFF&DEV_0007", 0, "Microsoft", "10.0.26100.1", new(2006, 6, 21), "stornvme.inf"),
        Device("USB xHCI Compliant Host Controller (demo)", "USB", @"PCI\VEN_FFFF&DEV_0008", 0, "Microsoft", "10.0.26100.1", new(2006, 6, 21), "usbxhci.inf"),
        Device("HID Keyboard Device (demo)", "Keyboard", @"HID\VID_FFFF&PID_0009", 0, "Microsoft", "10.0.26100.1", new(2006, 6, 21), "keyboard.inf"),
        Device("Generic PnP Monitor (demo)", "Monitor", @"MONITOR\FFF0001", 0, "Microsoft", "10.0.26100.1", new(2006, 6, 21), "monitor.inf"),
        Device("Contoso Card Reader (demo)", null, @"PCI\VEN_FFFF&DEV_000A", 28, null, null, null, null),
        Device("Contoso Fingerprint Sensor (demo)", "Biometric", @"USB\VID_FFFF&PID_000B", 10, "Contoso", "1.0.0.12", new(2021, 4, 9), "oem16.inf"),
        Device("Contoso Webcam (demo)", "Camera", @"USB\VID_FFFF&PID_000C", 22, "Contoso", "2.1.3.0", new(2022, 9, 30), "oem17.inf"),
    ];

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ScanResult { Devices = CreateDevices(), ScannedAt = DateTimeOffset.Now });

    private static DeviceInfo Device(string name, string? pnpClass, string hwid, int problem,
        string? provider, string? version, DateTime? date, string? inf) => new()
        {
            InstanceId = hwid + @"\DEMO&0",
            Name = name,
            Manufacturer = provider ?? "",
            DeviceClass = pnpClass ?? "",
            HardwareIds = [hwid],
            CompatibleIds = [],
            ProblemCode = problem,
            State = DeviceCategorizer.StateFromProblemCode(problem),
            Category = DeviceCategorizer.Categorize(pnpClass, name),
            VendorId = "FFFF",
            Driver = inf is null ? null : new DriverDetails
            {
                Provider = provider ?? "",
                Version = version ?? "",
                Date = date,
                InfName = inf,
                IsSigned = true,
                Signer = "Demo signer",
            },
        };
}

/// <summary>Simulated system information for demo mode.</summary>
public sealed class MockSystemInfoProvider : ISystemInfoProvider
{
    public Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SystemInfo
        {
            WindowsVersion = "Windows 11 Pro 24H2 (demo)",
            WindowsBuild = "26100.1",
            Architecture = "x64 (64-bit)",
            Manufacturer = "Contoso (demo)",
            Model = "Contoso Laptop 15 (demo)",
        });
}

/// <summary>Simulated driver store for demo mode.</summary>
public sealed class MockDriverStoreReader : IDriverStoreReader
{
    public IReadOnlyList<DriverPackageInfo> GetThirdPartyPackages() =>
        MockHardwareProvider.CreateDevices()
            .Where(d => d.Driver?.IsThirdParty == true)
            .Select(d => new DriverPackageInfo
            {
                PublishedName = d.Driver!.InfName,
                Provider = d.Driver.Provider,
                Version = d.Driver.Version,
                Date = d.Driver.Date,
                Class = d.DeviceClass,
            })
            .ToList();
}
