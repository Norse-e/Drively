using System.Text;
using System.Text.Json;
using Drively.Core.Models;

namespace Drively.Core.Services;

/// <summary>Exports the detected driver inventory to CSV or JSON for the user's own records.</summary>
public static class InventoryExporter
{
    private static readonly string[] Header =
        ["Category", "Device", "Manufacturer", "Class", "Status", "ProblemCode", "HardwareId",
         "DriverProvider", "DriverVersion", "DriverDate", "InfName", "Signed"];

    public static string ToCsv(IEnumerable<DeviceInfo> devices)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Header));
        foreach (var d in devices)
        {
            string[] row =
            [
                d.CategoryName, d.Name, d.Manufacturer, d.DeviceClass, d.StateName, d.ProblemCode.ToString(),
                d.PrimaryHardwareId, d.Driver?.Provider ?? "", d.Driver?.Version ?? "",
                d.Driver?.Date?.ToString("yyyy-MM-dd") ?? "", d.Driver?.InfName ?? "",
                d.Driver?.IsSigned switch { true => "Yes", false => "No", _ => "" },
            ];
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        }
        return sb.ToString();
    }

    public static string ToJson(IEnumerable<DeviceInfo> devices, SystemInfo? system, DateTimeOffset scannedAt)
    {
        var payload = new
        {
            exportedBy = "DRIVELY",
            scannedAt,
            system,
            devices = devices.Select(d => new
            {
                category = d.CategoryName,
                name = d.Name,
                manufacturer = d.Manufacturer,
                deviceClass = d.DeviceClass,
                status = d.StateName,
                problemCode = d.ProblemCode,
                hardwareIds = d.HardwareIds,
                driver = d.Driver is null ? null : new
                {
                    provider = d.Driver.Provider,
                    version = d.Driver.Version,
                    date = d.Driver.Date?.ToString("yyyy-MM-dd"),
                    infName = d.Driver.InfName,
                    isSigned = d.Driver.IsSigned,
                    signer = d.Driver.Signer,
                },
            }),
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Escape(string value)
    {
        // Guard against CSV formula injection when opened in spreadsheet apps.
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
