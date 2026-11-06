using System.Text.RegularExpressions;
using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.DriverInstallation;

/// <summary>
/// Thin wrapper around the built-in Windows PnPUtil tool (%SystemRoot%\System32\pnputil.exe).
/// Arguments are passed as a list, never through a shell. Both operations require administrator rights.
/// </summary>
public sealed partial class PnpUtilClient(IProcessRunner runner)
{
    public static string PnpUtilPath => Path.Combine(Environment.SystemDirectory, "pnputil.exe");

    /// <summary>Exports one published third-party driver package (oemNN.inf) to a folder.</summary>
    public Task<ProcessResult> ExportDriverAsync(string publishedName, string destinationFolder, CancellationToken cancellationToken = default)
    {
        if (!PublishedNameRegex().IsMatch(publishedName))
            throw new ArgumentException("Only published third-party driver packages (oemNN.inf) can be exported.", nameof(publishedName));

        Directory.CreateDirectory(destinationFolder);
        return runner.RunAsync(PnpUtilPath, ["/export-driver", publishedName, destinationFolder], cancellationToken);
    }

    /// <summary>Adds a driver package to the driver store and installs it on matching devices.</summary>
    public Task<ProcessResult> AddDriverAsync(string infPath, bool install, CancellationToken cancellationToken = default)
    {
        if (!infPath.EndsWith(".inf", StringComparison.OrdinalIgnoreCase) || !Path.IsPathFullyQualified(infPath))
            throw new ArgumentException("A full path to an .inf file is required.", nameof(infPath));

        List<string> args = ["/add-driver", infPath];
        if (install) args.Add("/install");
        return runner.RunAsync(PnpUtilPath, args, cancellationToken);
    }

    [GeneratedRegex(@"^oem\d+\.inf$", RegexOptions.IgnoreCase)]
    private static partial Regex PublishedNameRegex();
}

/// <summary>Interprets pnputil exit codes into honest, user-facing results.</summary>
public static class PnpUtilResults
{
    public const int Success = 0;
    public const int RebootRequired = 3010;   // ERROR_SUCCESS_REBOOT_REQUIRED
    public const int NoMoreItems = 259;       // ERROR_NO_MORE_ITEMS: package staged, not installed on any device
    public const int AccessDenied = 5;

    public static OperationResult InterpretAddDriver(ProcessResult result)
    {
        var details = $"pnputil exit code {result.ExitCode}.{Environment.NewLine}{result.StandardOutput}{result.StandardError}".Trim();
        return result.ExitCode switch
        {
            Success => OperationResult.Ok("Driver installed successfully.", details: details),
            RebootRequired => OperationResult.Ok("Driver installed. A restart is required to finish installation.", rebootRequired: true, details: details),
            NoMoreItems => OperationResult.Ok(
                "The driver package was added to Windows, but Windows did not apply it to any device " +
                "(a same or better-ranked driver is already in use, or no matching device is connected).", details: details),
            AccessDenied => OperationResult.Fail("Administrator permissions are required.", details),
            unchecked((int)0xE000022F) => OperationResult.Fail("Installation failed: the driver package has no valid catalog for this INF.", details),
            unchecked((int)0x800B0100) or unchecked((int)0x800B0109) or unchecked((int)0xE0000247) =>
                OperationResult.Fail("Driver package signature verification failed. Windows refused to install it.", details),
            _ => OperationResult.Fail($"Installation failed (pnputil exit code {result.ExitCode}).", details),
        };
    }
}
