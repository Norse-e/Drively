using System.Globalization;
using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.Data.Repositories;

/// <summary>Stores driver operation history locally in SQLite.</summary>
public sealed class HistoryRepository(DatabaseContext db) : IHistoryRepository
{
    private const int MaxRows = 5000;

    public async Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = db.Open();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO history (timestamp, device, driver_version, operation, result, details)
            VALUES ($ts, $device, $version, $op, $result, $details);
            """;
        command.Parameters.AddWithValue("$ts", entry.Timestamp.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$device", entry.Device);
        command.Parameters.AddWithValue("$version", entry.DriverVersion);
        command.Parameters.AddWithValue("$op", entry.Operation.ToString());
        command.Parameters.AddWithValue("$result", entry.Result.ToString());
        command.Parameters.AddWithValue("$details", entry.Details);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HistoryEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = db.Open();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT id, timestamp, device, driver_version, operation, result, details FROM history ORDER BY timestamp DESC, id DESC LIMIT {MaxRows};";

        var entries = new List<HistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new HistoryEntry
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                Device = reader.GetString(2),
                DriverVersion = reader.GetString(3),
                Operation = Enum.TryParse<HistoryOperation>(reader.GetString(4), out var op) ? op : HistoryOperation.Install,
                Result = Enum.TryParse<HistoryResult>(reader.GetString(5), out var res) ? res : HistoryResult.Failed,
                Details = reader.GetString(6),
            });
        }
        return entries;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = db.Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM history;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
