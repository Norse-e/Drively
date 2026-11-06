using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.Core.Services;

namespace Drively.DriverUpdates;

/// <summary>
/// Searches trusted update sources and keeps only updates with sufficient evidence of compatibility
/// (see <see cref="CompatibilityEvaluator"/>). If a source cannot be searched, the check fails with
/// an explanation instead of reporting "no updates".
/// </summary>
public sealed class UpdateChecker(
    IEnumerable<IUpdateProvider> providers,
    DriverRepository repository,
    INetworkStatus network,
    ILog log)
{
    public async Task<UpdateCheckResult> CheckAsync(IReadOnlyList<DeviceInfo> devices, CancellationToken cancellationToken = default)
    {
        if (!network.IsNetworkAvailable)
            return Store(Failed("No internet connection."));

        var updates = new List<DriverUpdate>();
        var rejections = new List<string>();
        var notes = new List<string>();

        foreach (var provider in providers)
        {
            UpdateSearchResult search;
            try
            {
                search = await provider.SearchAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Store(Failed("The update check was cancelled."));
            }
            catch (Exception ex)
            {
                log.Error($"{provider.Name} search threw an exception.", ex);
                search = UpdateSearchResult.Failure($"{provider.Name} could not be searched.");
            }

            if (!search.Succeeded)
                return Store(Failed($"Update discovery could not be completed: {search.ErrorMessage}"));

            if (search.Note is not null) notes.Add(search.Note);

            foreach (var candidate in search.Candidates)
            {
                var decision = CompatibilityEvaluator.Evaluate(candidate, devices);
                if (!decision.IsCompatible)
                {
                    rejections.Add(decision.Reason);
                    continue;
                }

                // One update per device: keep the first offer if a source lists duplicates.
                if (updates.Any(u => u.Device.InstanceId == decision.Device!.InstanceId && u.Id == candidate.Id)) continue;

                updates.Add(new DriverUpdate
                {
                    Candidate = candidate,
                    Device = decision.Device!,
                    MatchedId = decision.MatchedId!,
                    MatchKind = decision.MatchKind,
                });
            }
        }

        foreach (var reason in rejections) log.Info("Update not shown: " + reason);

        var message = updates.Count == 0
            ? UpdateCheckResult.NoUpdatesMessage
            : $"{updates.Count} verified driver update{(updates.Count == 1 ? "" : "s")} available.";
        if (notes.Count > 0) message += " " + string.Join(" ", notes);

        return Store(new UpdateCheckResult
        {
            Status = UpdateCheckStatus.Completed,
            Updates = updates,
            Message = message,
            RejectedCount = rejections.Count,
            RejectionReasons = rejections,
        });
    }

    private UpdateCheckResult Store(UpdateCheckResult result)
    {
        repository.Store(result);
        return result;
    }

    private static UpdateCheckResult Failed(string message) =>
        new() { Status = UpdateCheckStatus.Failed, Message = message };
}
