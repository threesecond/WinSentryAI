using Microsoft.Data.Sqlite;

namespace WinSentryAI.Services
{
    internal sealed record DatabaseMigration(int Version, string Name, string Sql);

    internal static class DatabaseMigrationRunner
    {
        public const int CurrentSchemaVersion = 1;

        private static readonly IReadOnlyList<DatabaseMigration> DefaultMigrations =
        [
            new DatabaseMigration(1, "Initial schema", InitialSchemaSql)
        ];

        public static void Run(SqliteConnection connection) => Run(connection, DefaultMigrations);

        internal static void Run(SqliteConnection connection, IReadOnlyList<DatabaseMigration> migrations)
        {
            int currentVersion = GetUserVersion(connection);

            foreach (var migration in migrations.OrderBy(m => m.Version))
            {
                if (migration.Version <= currentVersion)
                    continue;

                using var transaction = connection.BeginTransaction();
                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = migration.Sql;
                        command.ExecuteNonQuery();
                    }

                    using (var versionCommand = connection.CreateCommand())
                    {
                        versionCommand.Transaction = transaction;
                        versionCommand.CommandText = $"PRAGMA user_version = {migration.Version};";
                        versionCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    currentVersion = migration.Version;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public static int GetUserVersion(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private const string InitialSchemaSql = @"
            -- 主要事件表
            CREATE TABLE IF NOT EXISTS Events (
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

            -- 去重唯一索引
            CREATE UNIQUE INDEX IF NOT EXISTS idx_events_unique ON Events(EventId, Source, Timestamp);

            -- 上下文日誌
            CREATE TABLE IF NOT EXISTS ContextLogs (
                Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                TriggerEventId INTEGER NOT NULL REFERENCES Events(Id) ON DELETE CASCADE,
                Source         TEXT    NOT NULL,
                Level          INTEGER NOT NULL,
                EventId        INTEGER NOT NULL,
                Message        TEXT,
                Timestamp      DATETIME NOT NULL
            );

            -- AI 分析結果
            CREATE TABLE IF NOT EXISTS AnalysisResults (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                EventId      INTEGER NOT NULL REFERENCES Events(Id) ON DELETE CASCADE,
                AiModel      TEXT    NOT NULL,
                ModelName    TEXT,
                Prompt       TEXT,
                Response     TEXT,
                IsSuccess    INTEGER DEFAULT 1,
                ErrorMessage TEXT,
                CreatedAt    DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            -- 應用程式設定
            CREATE TABLE IF NOT EXISTS AppSettings (
                Key         TEXT PRIMARY KEY,
                Value       TEXT,
                IsEncrypted INTEGER DEFAULT 0
            );

            -- 系統環境快照
            CREATE TABLE IF NOT EXISTS SystemSnapshots (
                Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                OsVersion          TEXT,
                OsBuild            TEXT,
                ComputerName       TEXT,
                DomainOrWorkgroup  TEXT,
                IpAddresses        TEXT,
                TotalRamMb         INTEGER,
                CpuName            TEXT,
                GpuInfo            TEXT,
                CapturedAt         DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            -- 索引
            CREATE INDEX IF NOT EXISTS idx_events_timestamp ON Events(Timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_events_level     ON Events(Level);
            CREATE INDEX IF NOT EXISTS idx_events_host      ON Events(Host);
            CREATE INDEX IF NOT EXISTS idx_context_trigger  ON ContextLogs(TriggerEventId);
            CREATE INDEX IF NOT EXISTS idx_analysis_event   ON AnalysisResults(EventId);
        ";
    }
}
