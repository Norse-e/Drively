using Drively.Core.Models;

namespace Drively.DriverUpdates;

/// <summary>
/// In-memory store of the latest verified update check. Nothing is persisted or cached between runs,
/// so stale results are never shown after a restart.
/// </summary>
public sealed class DriverRepository
{
    private readonly object _gate = new();
    private UpdateCheckResult? _latest;

    public UpdateCheckResult? Latest
    {
        get { lock (_gate) return _latest; }
    }

    public void Store(UpdateCheckResult result)
    {
        lock (_gate) _latest = result;
    }

    public DriverUpdate? Find(string updateId)
    {
        lock (_gate) return _latest?.Updates.FirstOrDefault(u => u.Id == updateId);
    }

    public void Clear()
    {
        lock (_gate) _latest = null;
    }
}
