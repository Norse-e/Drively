using System.Management;

namespace Drively.DriverDetection;

/// <summary>Raw Plug and Play entity as reported by WMI (Win32_PnPEntity).</summary>
public sealed record PnpEntityRecord(
    string InstanceId,
    string? Name,
    string? Manufacturer,
    string? PnpClass,
    string? ClassGuid,
    int ProblemCode,
    string? Status,
    IReadOnlyList<string> HardwareIds,
    IReadOnlyList<string> CompatibleIds);

/// <summary>Enumerates present Plug and Play devices. Read-only; makes no changes.</summary>
public sealed class HardwareScanner
{
    private const string Query =
        "SELECT PNPDeviceID, Name, Manufacturer, PNPClass, ClassGuid, ConfigManagerErrorCode, Status, HardwareID, CompatibleID " +
        "FROM Win32_PnPEntity";

    public IReadOnlyList<PnpEntityRecord> Scan(CancellationToken cancellationToken = default)
    {
        var results = new List<PnpEntityRecord>();
        using var searcher = new ManagementObjectSearcher(@"root\CIMV2", Query);
        using var collection = searcher.Get();

        foreach (var item in collection)
        {
            using var mo = (ManagementObject)item;
            cancellationToken.ThrowIfCancellationRequested();

            var id = Wmi.String(mo, "PNPDeviceID");
            if (string.IsNullOrWhiteSpace(id)) continue;

            results.Add(new PnpEntityRecord(
                id,
                Wmi.String(mo, "Name"),
                Wmi.String(mo, "Manufacturer"),
                Wmi.String(mo, "PNPClass"),
                Wmi.String(mo, "ClassGuid"),
                Wmi.Int(mo, "ConfigManagerErrorCode"),
                Wmi.String(mo, "Status"),
                Wmi.StringArray(mo, "HardwareID"),
                Wmi.StringArray(mo, "CompatibleID")));
        }

        return results;
    }
}

internal static class Wmi
{
    public static string? String(ManagementBaseObject mo, string property)
    {
        try
        {
            return mo[property]?.ToString()?.Trim();
        }
        catch (ManagementException)
        {
            return null; // Property not available on this Windows build.
        }
    }

    public static int Int(ManagementBaseObject mo, string property)
    {
        try
        {
            return mo[property] is { } v ? Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture) : 0;
        }
        catch (Exception ex) when (ex is ManagementException or InvalidCastException or OverflowException or FormatException)
        {
            return 0;
        }
    }

    public static bool? Bool(ManagementBaseObject mo, string property)
    {
        try
        {
            return mo[property] is bool b ? b : null;
        }
        catch (ManagementException)
        {
            return null;
        }
    }

    public static IReadOnlyList<string> StringArray(ManagementBaseObject mo, string property)
    {
        try
        {
            return mo[property] is string[] values
                ? values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray()
                : [];
        }
        catch (ManagementException)
        {
            return [];
        }
    }

    public static DateTime? Date(ManagementBaseObject mo, string property)
    {
        var raw = String(mo, property);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            return ManagementDateTimeConverter.ToDateTime(raw).Date;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or FormatException)
        {
            return null;
        }
    }
}
