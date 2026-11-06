using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.DriverUpdates.Mock;

/// <summary>
/// Demo-mode update source (DRIVELY --demo). Offers one clearly-labelled simulated update for the
/// fictional Contoso hardware so the download/install UI can be exercised. It never downloads or
/// installs anything and is never registered outside demo mode.
/// </summary>
public sealed class MockUpdateProvider : IUpdateProvider
{
    public string Name => "Demo source (simulated)";

    public string NetworkDisclosure => "Demo mode: no network connections are made.";

    public async Task<UpdateSearchResult> SearchAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(800, cancellationToken).ConfigureAwait(false);
        return UpdateSearchResult.Success(
        [
            new UpdateCandidate
            {
                Id = "demo-graphics-update",
                Title = "Contoso - Display - 31.0.15.2000 (demo)",
                HardwareId = @"PCI\VEN_FFFF&DEV_0001",
                Manufacturer = "Contoso",
                Provider = "Contoso",
                DriverClass = "Display",
                Version = "31.0.15.2000",
                DriverDate = new DateTime(2025, 2, 1),
                DownloadSizeBytes = 512L * 1024 * 1024,
                Source = Name,
                SignatureStatus = "Simulated (demo mode)",
            },
        ]);
    }

    public async Task<OperationResult> DownloadAsync(UpdateCandidate update, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1200, cancellationToken).ConfigureAwait(false);
        return OperationResult.Ok("Demo mode: download simulated. Nothing was downloaded.");
    }

    public async Task<OperationResult> InstallAsync(UpdateCandidate update, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1200, cancellationToken).ConfigureAwait(false);
        return OperationResult.Ok("Demo mode: installation simulated. Nothing was installed.", rebootRequired: true);
    }
}
