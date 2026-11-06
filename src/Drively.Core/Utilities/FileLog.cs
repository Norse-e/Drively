using Drively.Core.Interfaces;

namespace Drively.Core.Utilities;

/// <summary>
/// Appends diagnostics to a local daily log file. Logs never leave the PC and old files are pruned.
/// Avoid logging personal data; device names and driver versions are fine.
/// </summary>
public sealed class FileLog : ILog
{
    private const int RetentionDays = 14;
    private readonly string _directory;
    private readonly object _gate = new();

    public FileLog(string directory)
    {
        _directory = directory;
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var file in Directory.EnumerateFiles(directory, "drively-*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-RetentionDays)) File.Delete(file);
            }
        }
        catch (Exception)
        {
            // Logging must never break the application.
        }
    }

    public string LogDirectory => _directory;

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";
        if (exception is not null) line += Environment.NewLine + "    " + exception.GetType().Name + ": " + exception.Message;

        try
        {
            lock (_gate)
            {
                File.AppendAllText(Path.Combine(_directory, $"drively-{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Ignore logging failures (disk full, permissions).
        }
    }
}

/// <summary>A log that discards everything (tests, design time).</summary>
public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();
    public void Info(string message) { }
    public void Warn(string message, Exception? exception = null) { }
    public void Error(string message, Exception? exception = null) { }
}
