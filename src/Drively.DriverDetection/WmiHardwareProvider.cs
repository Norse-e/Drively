using System.Management;
using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.DriverDetection;

/// <summary>Real hardware detection: combines <see cref="HardwareScanner"/> and <see cref="DriverScanner"/>.</summary>
public sealed class WmiHardwareProvider(ILog log) : IHardwareProvider
{
    private readonly HardwareScanner _hardware = new();
    private readonly DriverScanner _drivers = new();

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private ScanResult Scan(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.Now;
        IReadOnlyList<PnpEntityRecord> entities;
        try
        {
            entities = _hardware.Scan(cancellationToken);
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            log.Error("Hardware scan failed.", ex);
            throw new DetectionException("Unable to detect devices. Windows Management Instrumentation (WMI) is not responding.", ex);
        }

        var warnings = new List<string>();
        IReadOnlyDictionary<string, SignedDriverRecord> drivers;
        try
        {
            drivers = _drivers.Scan(cancellationToken);
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            log.Warn("Driver details scan failed; continuing without driver details.", ex);
            warnings.Add("Driver details could not be read. Device list is shown without driver versions.");
            drivers = new Dictionary<string, SignedDriverRecord>();
        }

        var devices = entities
            .Select(e => DeviceIdentifier.Build(e, drivers.GetValueOrDefault(e.InstanceId)))
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        log.Info($"Scan completed: {devices.Count} devices, {drivers.Count} driver records in {(DateTimeOffset.Now - started).TotalSeconds:0.0}s.");
        return new ScanResult { Devices = devices, ScannedAt = DateTimeOffset.Now, Warnings = warnings };
    }
}

/// <summary>Raised when devices cannot be detected at all.</summary>
public sealed class DetectionException(string message, Exception? inner = null) : Exception(message, inner);
