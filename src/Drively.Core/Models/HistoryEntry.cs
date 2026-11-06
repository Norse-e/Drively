namespace Drively.Core.Models;

public enum HistoryOperation
{
    Download,
    Install,
    Backup,
    Restore,
}

public enum HistoryResult
{
    Succeeded,
    Failed,
    Cancelled,
}

/// <summary>A locally stored record of a driver operation.</summary>
public sealed record HistoryEntry
{
    public long Id { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string Device { get; init; } = "";
    public string DriverVersion { get; init; } = "";
    public HistoryOperation Operation { get; init; }
    public HistoryResult Result { get; init; }
    public string Details { get; init; } = "";

    public string TimestampText => Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}
