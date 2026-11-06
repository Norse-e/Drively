using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.DriverInstallation;

/// <summary>
/// Orchestrates download and installation of verified updates. It never installs anything on its own:
/// callers invoke it only after the user explicitly confirmed. Every outcome is written to history.
/// </summary>
public sealed class InstallationManager(
    IEnumerable<IUpdateProvider> providers,
    IElevationService elevation,
    IHistoryRepository history,
    ILog log)
{
    public const string AdminRequiredMessage =
        "Administrator permissions are required. Use \"Restart as administrator\" and try again.";

    public async Task<OperationResult> DownloadAsync(DriverUpdate update, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(update, (p, u, ct) => p.DownloadAsync(u, ct), cancellationToken).ConfigureAwait(false);
        await RecordAsync(update, HistoryOperation.Download, result).ConfigureAwait(false);
        if (result.Success) update.Candidate.IsDownloaded = true;
        return result;
    }

    /// <summary>Downloads (if needed) and installs. Call only after explicit user confirmation.</summary>
    public async Task<OperationResult> DownloadAndInstallAsync(DriverUpdate update, CancellationToken cancellationToken = default)
    {
        if (!update.Candidate.IsDownloaded)
        {
            var download = await DownloadAsync(update, cancellationToken).ConfigureAwait(false);
            if (!download.Success) return download;
        }

        var result = await RunAsync(update, (p, u, ct) => p.InstallAsync(u, ct), cancellationToken).ConfigureAwait(false);
        await RecordAsync(update, HistoryOperation.Install, result).ConfigureAwait(false);
        return result;
    }

    private async Task<OperationResult> RunAsync(
        DriverUpdate update,
        Func<IUpdateProvider, UpdateCandidate, CancellationToken, Task<OperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!elevation.IsElevated) return OperationResult.Fail(AdminRequiredMessage);

        var provider = providers.FirstOrDefault(p => p.Name == update.Source);
        if (provider is null) return OperationResult.Fail($"The update source '{update.Source}' is not available.");

        try
        {
            return await operation(provider, update.Candidate, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail("The operation was cancelled.");
        }
        catch (Exception ex)
        {
            // Unexpected failures are reported as failures, never as success.
            log.Error($"Operation failed for '{update.Title}'.", ex);
            return OperationResult.Fail("Installation failed.", ex.Message);
        }
    }

    private async Task RecordAsync(DriverUpdate update, HistoryOperation operation, OperationResult result)
    {
        try
        {
            await history.AddAsync(new HistoryEntry
            {
                Timestamp = DateTimeOffset.Now,
                Device = update.DeviceName,
                DriverVersion = update.Candidate.Version ?? update.Title,
                Operation = operation,
                Result = result.Success ? HistoryResult.Succeeded : HistoryResult.Failed,
                Details = result.RebootRequired ? result.Message + " (restart required)" : result.Message,
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Warn("Could not write history entry.", ex);
        }
    }
}
