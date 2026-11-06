using Drively.Data.Migrations;
using Microsoft.Data.Sqlite;

namespace Drively.Data;

/// <summary>
/// Opens the local SQLite database and applies schema migrations (tracked with PRAGMA user_version).
/// The database lives in %LOCALAPPDATA%\Drively and is never synchronized anywhere.
/// </summary>
public sealed class DatabaseContext
{
    private readonly string _connectionString;

    public DatabaseContext(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        Migrate();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Migrate()
    {
        using var connection = Open();
        using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        var current = Convert.ToInt32(versionCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

        foreach (var migration in MigrationList.All.Where(m => m.Version > current).OrderBy(m => m.Version))
        {
            using var transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = migration.Sql + $"\nPRAGMA user_version = {migration.Version};";
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }
}
