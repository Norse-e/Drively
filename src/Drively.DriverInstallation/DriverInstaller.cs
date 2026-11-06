using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.DriverInstallation;

/// <summary>
/// Installs a local, validated driver package (INF + signed catalog) with PnPUtil.
/// Validation always runs first; nothing is installed if validation fails.
/// </summary>
public sealed class DriverInstaller(DriverValidator validator, PnpUtilClient pnpUtil, IElevationService elevation, ILog log)
{
    public async Task<OperationResult> InstallPackageAsync(string infPath, CancellationToken cancellationToken = default)
    {
        var validation = validator.Validate(infPath);
        if (!validation.IsValid)
        {
            log.Warn($"Validation failed for {Path.GetFileName(infPath)}: {string.Join(" ", validation.Errors)}");
            return OperationResult.Fail(validation.Errors[0], string.Join(Environment.NewLine, validation.Errors));
        }

        if (!elevation.IsElevated)
            return OperationResult.Fail("Administrator permissions are required.");

        try
        {
            var result = await pnpUtil.AddDriverAsync(Path.GetFullPath(infPath), install: true, cancellationToken).ConfigureAwait(false);
            var outcome = PnpUtilResults.InterpretAddDriver(result);
            log.Info($"pnputil /add-driver {Path.GetFileName(infPath)} -> exit {result.ExitCode}");
            return outcome;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            log.Error("Could not start pnputil.", ex);
            return OperationResult.Fail("Installation failed: the Windows PnPUtil tool could not be started.", ex.Message);
        }
    }
}
