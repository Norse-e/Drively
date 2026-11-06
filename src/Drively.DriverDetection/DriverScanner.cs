using System.Management;

namespace Drively.DriverDetection;

/// <summary>Driver bound to a device, as reported by WMI (Win32_PnPSignedDriver).</summary>
public sealed record SignedDriverRecord(
    string DeviceId,
    string? Provider,
    string? Version,
    DateTime? Date,
    string? InfName,
    bool? IsSigned,
    string? Signer);

/// <summary>Reads installed driver details for all devices. Read-only.</summary>
public sealed class DriverScanner
{
    private const string Query =
        "SELECT DeviceID, DriverProviderName, DriverVersion, DriverDate, InfName, IsSigned, Signer FROM Win32_PnPSignedDriver";

    public IReadOnlyDictionary<string, SignedDriverRecord> Scan(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, SignedDriverRecord>(StringComparer.OrdinalIgnoreCase);
        using var searcher = new ManagementObjectSearcher(@"root\CIMV2", Query);
        using var collection = searcher.Get();

        foreach (var item in collection)
        {
            using var mo = (ManagementObject)item;
            cancellationToken.ThrowIfCancellationRequested();

            var deviceId = Wmi.String(mo, "DeviceID");
            if (string.IsNullOrWhiteSpace(deviceId)) continue;

            var record = new SignedDriverRecord(
                deviceId,
                Wmi.String(mo, "DriverProviderName"),
                Wmi.String(mo, "DriverVersion"),
                Wmi.Date(mo, "DriverDate"),
                Wmi.String(mo, "InfName"),
                Wmi.Bool(mo, "IsSigned"),
                Wmi.String(mo, "Signer"));

            // A device has at most one bound driver; keep the first record with real data.
            if (!results.TryGetValue(deviceId, out var existing) || !HasDriver(existing))
                results[deviceId] = record;
        }

        return results;
    }

    public static bool HasDriver(SignedDriverRecord record) =>
        !string.IsNullOrWhiteSpace(record.InfName) || !string.IsNullOrWhiteSpace(record.Version);
}
