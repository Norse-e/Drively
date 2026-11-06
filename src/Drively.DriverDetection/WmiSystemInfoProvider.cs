using System.Management;
using System.Runtime.InteropServices;
using Drively.Core.Interfaces;
using Drively.Core.Models;
using Microsoft.Win32;

namespace Drively.DriverDetection;

/// <summary>Reads Windows version and PC manufacturer/model. Read-only, local only.</summary>
public sealed class WmiSystemInfoProvider(ILog log) : ISystemInfoProvider
{
    public Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default) =>
        Task.Run(Read, cancellationToken);

    private SystemInfo Read()
    {
        string caption = "Windows", build = "", manufacturer = "Unknown", model = "Unknown", boardMfr = "", boardProduct = "";
        try
        {
            using (var os = new ManagementObjectSearcher("SELECT Caption, BuildNumber FROM Win32_OperatingSystem"))
            using (var results = os.Get())
            {
                foreach (var mo in results)
                {
                    caption = Wmi.String(mo, "Caption")?.Replace("Microsoft ", "", StringComparison.Ordinal) ?? caption;
                    build = Wmi.String(mo, "BuildNumber") ?? "";
                }
            }

            using (var cs = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
            using (var results = cs.Get())
            {
                foreach (var mo in results)
                {
                    manufacturer = Wmi.String(mo, "Manufacturer") ?? manufacturer;
                    model = Wmi.String(mo, "Model") ?? model;
                }
            }

            using (var bb = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
            using (var results = bb.Get())
            {
                foreach (var mo in results)
                {
                    boardMfr = Wmi.String(mo, "Manufacturer") ?? "";
                    boardProduct = Wmi.String(mo, "Product") ?? "";
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            log.Warn("Could not read all system information.", ex);
        }

        var (displayVersion, ubr) = ReadVersionFromRegistry();
        var version = string.IsNullOrEmpty(displayVersion) ? caption : $"{caption} {displayVersion}";
        var buildText = string.IsNullOrEmpty(ubr) ? build : $"{build}.{ubr}";

        return new SystemInfo
        {
            WindowsVersion = version,
            WindowsBuild = buildText,
            Architecture = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64 (64-bit)",
                Architecture.Arm64 => "ARM64",
                Architecture.X86 => "x86 (32-bit)",
                var other => other.ToString(),
            },
            Manufacturer = manufacturer,
            Model = model,
            BaseBoardManufacturer = boardMfr,
            BaseBoardProduct = boardProduct,
        };
    }

    private static (string? DisplayVersion, string? Ubr) ReadVersionFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return (key?.GetValue("DisplayVersion") as string, key?.GetValue("UBR")?.ToString());
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return (null, null);
        }
    }
}
