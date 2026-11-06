using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using Drively.Core.Interfaces;

namespace Drively.App.Services;

/// <summary>Detects administrator rights and relaunches DRIVELY elevated on request (UAC prompt).</summary>
public sealed class ElevationService(AppEnvironment environment) : IElevationService
{
    public bool IsElevated { get; } = CheckElevated();

    public bool TryRestartElevated()
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return false;

        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = environment.IsDemoMode ? "--demo" : "",
            };
            Process.Start(psi);
            Application.Current.Shutdown();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false; // ERROR_CANCELLED: the user declined the UAC prompt.
        }
    }

    private static bool CheckElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

/// <summary>Process-wide facts decided at startup.</summary>
public sealed record AppEnvironment(bool IsDemoMode, string Version);
