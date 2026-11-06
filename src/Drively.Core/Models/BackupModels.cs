namespace Drively.Core.Models;

/// <summary>A third-party driver package in the Windows driver store (oemNN.inf).</summary>
public sealed record DriverPackageInfo
{
    public required string PublishedName { get; init; }
    public string InfPath { get; init; } = "";
    public string Provider { get; init; } = "";
    public string Version { get; init; } = "";
    public DateTime? Date { get; init; }
    public string Class { get; init; } = "";
}

/// <summary>One driver package inside a backup. Serialized to the backup manifest (JSON).</summary>
public sealed class BackupEntry
{
    public string PublishedInf { get; set; } = "";
    public string OriginalInf { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime? DriverDate { get; set; }
    public string Class { get; set; } = "";
    public List<string> DeviceNames { get; set; } = [];

    /// <summary>Folder relative to the backup root that contains the exported package.</summary>
    public string Folder { get; set; } = "";

    public bool Exported { get; set; }
    public string? Error { get; set; }

    public string DeviceNamesText => DeviceNames.Count == 0 ? "(no connected device uses this driver)" : string.Join(", ", DeviceNames);
}

/// <summary>The backup manifest written next to the exported packages.</summary>
public sealed class BackupManifest
{
    public int FormatVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string AppVersion { get; set; } = "";
    public string WindowsVersion { get; set; } = "";
    public string ComputerModel { get; set; } = "";
    public List<BackupEntry> Entries { get; set; } = [];
}

/// <summary>A backup found on disk.</summary>
public sealed record BackupInfo(string FolderPath, BackupManifest Manifest)
{
    public string Name => Path.GetFileName(FolderPath);
    public int ExportedCount => Manifest.Entries.Count(e => e.Exported);
    public string CreatedText => Manifest.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}

public sealed record BackupResult(bool Success, string Message, BackupInfo? Backup);

public sealed record RestoreItemResult(BackupEntry Entry, OperationResult Result);
