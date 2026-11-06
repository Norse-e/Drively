namespace Drively.Core.Models;

/// <summary>
/// A raw driver update offered by an update source (e.g. Windows Update), before DRIVELY has
/// matched it to a device on this PC.
/// </summary>
public sealed class UpdateCandidate
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = "";
    public string HardwareId { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Provider { get; init; } = "";
    public string DriverClass { get; init; } = "";
    public string Model { get; init; } = "";

    /// <summary>Driver version, if the source reports one. Null when unknown — never guessed.</summary>
    public string? Version { get; init; }

    public DateTime? DriverDate { get; init; }
    public long? DownloadSizeBytes { get; init; }
    public string? SupportUrl { get; init; }
    public bool IsDownloaded { get; set; }
    public bool EulaAccepted { get; init; } = true;

    /// <summary>Name of the <see cref="Interfaces.IUpdateProvider"/> that produced this candidate.</summary>
    public required string Source { get; init; }

    /// <summary>Human-readable signature status as known for this source.</summary>
    public string SignatureStatus { get; init; } = "Unknown";

    /// <summary>Opaque provider object (e.g. the Windows Update COM object). Not serialized.</summary>
    public object? ProviderState { get; init; }
}

public enum MatchKind
{
    HardwareId,
    CompatibleId,
}

/// <summary>An update that DRIVELY verified as compatible with a specific present device.</summary>
public sealed class DriverUpdate
{
    public required UpdateCandidate Candidate { get; init; }
    public required DeviceInfo Device { get; init; }
    public required string MatchedId { get; init; }
    public MatchKind MatchKind { get; init; }

    public string Id => Candidate.Id;
    public string Title => Candidate.Title;
    public string DeviceName => Device.Name;
    public string CurrentVersion => Device.Driver?.Version is { Length: > 0 } v ? v : "No driver installed";
    public string NewVersion => Candidate.Version ?? "Not reported by source";
    public string Provider => string.IsNullOrWhiteSpace(Candidate.Provider) ? Candidate.Manufacturer : Candidate.Provider;
    public string Source => Candidate.Source;
    public string SignatureStatus => Candidate.SignatureStatus;
    public string NewDriverDateText => Candidate.DriverDate is { } d ? d.ToString("yyyy-MM-dd") : "—";
    public string CurrentDriverDateText => Device.DriverDateText;

    /// <summary>
    /// Explains when the update replaces a driver from a different provider (e.g. Microsoft inbox → Intel).
    /// Version numbers from different providers are not directly comparable, so this is shown openly.
    /// </summary>
    public string ProviderChangeText
    {
        get
        {
            var current = Device.Driver?.Provider;
            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(Provider)) return "";
            if (current.Contains(Provider, StringComparison.OrdinalIgnoreCase) || Provider.Contains(current, StringComparison.OrdinalIgnoreCase)) return "";
            return $"Replaces the current driver from {current} with one from {Provider}. Version numbers from different providers are not directly comparable; Windows Update determined this driver is applicable to the device.";
        }
    }
    public string SizeText => Candidate.DownloadSizeBytes is { } s and > 0 ? Utilities.ByteSize.Format(s) : "Not reported";
}

public enum UpdateCheckStatus
{
    Completed,
    Failed,
}

/// <summary>Outcome of searching one source for driver updates.</summary>
public sealed class UpdateSearchResult
{
    public bool Succeeded { get; init; }
    public IReadOnlyList<UpdateCandidate> Candidates { get; init; } = [];
    public string? ErrorMessage { get; init; }

    /// <summary>Extra context shown to the user when the result needs explanation (e.g. policy).</summary>
    public string? Note { get; init; }

    public static UpdateSearchResult Success(IReadOnlyList<UpdateCandidate> candidates, string? note = null) =>
        new() { Succeeded = true, Candidates = candidates, Note = note };

    public static UpdateSearchResult Failure(string message) => new() { Succeeded = false, ErrorMessage = message };
}

/// <summary>Outcome of a full update check, after compatibility validation.</summary>
public sealed class UpdateCheckResult
{
    public const string NoUpdatesMessage = "No verified driver updates available.";

    public UpdateCheckStatus Status { get; init; }
    public IReadOnlyList<DriverUpdate> Updates { get; init; } = [];
    public string Message { get; init; } = "";
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>Number of offered updates rejected by compatibility validation.</summary>
    public int RejectedCount { get; init; }

    /// <summary>Why each rejected update was rejected (for transparency and logs).</summary>
    public IReadOnlyList<string> RejectionReasons { get; init; } = [];

    public bool Succeeded => Status == UpdateCheckStatus.Completed;
}
