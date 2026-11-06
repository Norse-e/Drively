namespace Drively.Core.Utilities;

/// <summary>Local storage locations. Everything DRIVELY stores lives on this PC.</summary>
public static class AppPaths
{
    public static string DataRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Drively");

    public static string DatabasePath => Path.Combine(DataRoot, "drively.db");
    public static string LogDirectory => Path.Combine(DataRoot, "logs");

    public static string DefaultBackupDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DRIVELY", "Backups");

    public static string DefaultDownloadDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "DRIVELY");
}
