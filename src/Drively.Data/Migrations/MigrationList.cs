namespace Drively.Data.Migrations;

public sealed record Migration(int Version, string Sql);

/// <summary>Ordered schema migrations. Never edit a released migration; add a new one.</summary>
public static class MigrationList
{
    public static readonly IReadOnlyList<Migration> All =
    [
        new(1, """
            CREATE TABLE IF NOT EXISTS history (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp      TEXT    NOT NULL,
                device         TEXT    NOT NULL,
                driver_version TEXT    NOT NULL,
                operation      TEXT    NOT NULL,
                result         TEXT    NOT NULL,
                details        TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_history_timestamp ON history(timestamp);

            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """),
    ];
}
