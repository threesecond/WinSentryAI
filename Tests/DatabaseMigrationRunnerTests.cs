using Microsoft.Data.Sqlite;
using WinSentryAI.Models;
using WinSentryAI.Services;

namespace WinSentryAI.Tests;

public class DatabaseMigrationRunnerTests
{
    [Fact]
    public async Task Initialize_creates_empty_database_and_sets_schema_version()
    {
        string dbPath = Path.Combine(TestPaths.CreateTempDirectory(), "empty.db");
        var service = new DatabaseService(dbPath);

        service.Initialize(retentionDays: 30);
        long id = await service.SaveEventAsync(new EventRecord
        {
            Source = "System",
            Level = EventLevel.Warning,
            EventId = 10016,
            Message = "COM permission warning",
            Timestamp = DateTime.UtcNow,
            Host = "localhost"
        });

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        Assert.True(id > 0);
        Assert.Equal(DatabaseMigrationRunner.CurrentSchemaVersion, DatabaseMigrationRunner.GetUserVersion(connection));
        Assert.True(TableExists(connection, "Events"));
        Assert.True(TableExists(connection, "AnalysisResults"));
    }

    [Fact]
    public void Initialize_upgrades_existing_v090_database_without_losing_data()
    {
        string dbPath = Path.Combine(TestPaths.CreateTempDirectory(), "v090.db");
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE Events (
                    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    Source        TEXT    NOT NULL,
                    Level         INTEGER NOT NULL,
                    EventId       INTEGER NOT NULL,
                    ProviderName  TEXT,
                    Message       TEXT,
                    Timestamp     DATETIME NOT NULL,
                    IsAnalyzed    INTEGER  DEFAULT 0,
                    Host          TEXT     DEFAULT 'localhost',
                    CreatedAt     DATETIME DEFAULT CURRENT_TIMESTAMP
                );
                INSERT INTO Events (Source, Level, EventId, Message, Timestamp)
                VALUES ('System', 3, 10016, 'existing event', '2026-04-30 22:00:00');
            ";
            command.ExecuteNonQuery();
            Assert.Equal(0, DatabaseMigrationRunner.GetUserVersion(connection));
        }

        var service = new DatabaseService(dbPath);
        service.Initialize(retentionDays: 3650);

        using var verify = new SqliteConnection($"Data Source={dbPath}");
        verify.Open();
        Assert.Equal(DatabaseMigrationRunner.CurrentSchemaVersion, DatabaseMigrationRunner.GetUserVersion(verify));
        Assert.Equal(1, CountRows(verify, "Events"));
        Assert.True(TableExists(verify, "ContextLogs"));
    }

    [Fact]
    public void Failed_migration_rolls_back_schema_changes_and_version()
    {
        string dbPath = Path.Combine(TestPaths.CreateTempDirectory(), "rollback.db");
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        var migrations = new[]
        {
            new DatabaseMigration(1, "Broken migration", @"
                CREATE TABLE RollbackProbe (Id INTEGER PRIMARY KEY);
                INSERT INTO MissingTable (Id) VALUES (1);
            ")
        };

        Assert.Throws<SqliteException>(() => DatabaseMigrationRunner.Run(connection, migrations));

        Assert.Equal(0, DatabaseMigrationRunner.GetUserVersion(connection));
        Assert.False(TableExists(connection, "RollbackProbe"));
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name";
        command.Parameters.AddWithValue("@name", tableName);
        return command.ExecuteScalar() != null;
    }

    private static int CountRows(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName}";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
