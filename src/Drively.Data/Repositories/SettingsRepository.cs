using System.Globalization;
using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.Data.Repositories;

/// <summary>Stores settings as key/value rows in SQLite.</summary>
public sealed class SettingsRepository(DatabaseContext db) : ISettingsRepository
{
    public AppSettings Load()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var connection = db.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT key, value FROM settings;";
            using var reader = command.ExecuteReader();
            while (reader.Read()) values[reader.GetString(0)] = reader.GetString(1);
        }

        var settings = new AppSettings();
        if (values.TryGetValue(nameof(AppSettings.Theme), out var theme) && Enum.TryParse<ThemePreference>(theme, out var t)) settings.Theme = t;
        if (values.TryGetValue(nameof(AppSettings.Language), out var lang)) settings.Language = lang;
        if (values.TryGetValue(nameof(AppSettings.DownloadDirectory), out var dl)) settings.DownloadDirectory = dl;
        if (values.TryGetValue(nameof(AppSettings.BackupDirectory), out var bk)) settings.BackupDirectory = bk;
        if (values.TryGetValue(nameof(AppSettings.CheckForUpdatesOnStartup), out var auto)) settings.CheckForUpdatesOnStartup = auto == "1";
        settings.LastScan = ParseDate(values.GetValueOrDefault(nameof(AppSettings.LastScan)));
        settings.LastUpdateCheck = ParseDate(values.GetValueOrDefault(nameof(AppSettings.LastUpdateCheck)));
        return settings;
    }

    public void Save(AppSettings settings)
    {
        var values = new Dictionary<string, string>
        {
            [nameof(AppSettings.Theme)] = settings.Theme.ToString(),
            [nameof(AppSettings.Language)] = settings.Language,
            [nameof(AppSettings.DownloadDirectory)] = settings.DownloadDirectory,
            [nameof(AppSettings.BackupDirectory)] = settings.BackupDirectory,
            [nameof(AppSettings.CheckForUpdatesOnStartup)] = settings.CheckForUpdatesOnStartup ? "1" : "0",
            [nameof(AppSettings.LastScan)] = settings.LastScan?.ToString("O", CultureInfo.InvariantCulture) ?? "",
            [nameof(AppSettings.LastUpdateCheck)] = settings.LastUpdateCheck?.ToString("O", CultureInfo.InvariantCulture) ?? "",
        };

        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var (key, value) in values)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO settings (key, value) VALUES ($k, $v) ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
            command.Parameters.AddWithValue("$k", key);
            command.Parameters.AddWithValue("$v", value);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
