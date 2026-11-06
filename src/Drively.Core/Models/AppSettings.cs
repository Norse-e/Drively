namespace Drively.Core.Models;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>Essential user settings, stored locally in SQLite.</summary>
public sealed class AppSettings
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public string Language { get; set; } = "en";
    public string DownloadDirectory { get; set; } = "";
    public string BackupDirectory { get; set; } = "";

    /// <summary>Off by default: DRIVELY only contacts Windows Update when asked.</summary>
    public bool CheckForUpdatesOnStartup { get; set; }

    public DateTimeOffset? LastScan { get; set; }
    public DateTimeOffset? LastUpdateCheck { get; set; }
}
