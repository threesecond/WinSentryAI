using Microsoft.Data.Sqlite;
using WinSentryAI.Models;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WinSentryAI.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public DatabaseService()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinSentryAI.db");
            _connectionString = $"Data Source={_dbPath}";
        }

        public DatabaseService(string dbPath)
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={_dbPath}";
        }

        public SqliteConnection GetConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public void Initialize(int retentionDays = 7)
        {
            using var connection = GetConnection();
            
            // 初始化 PRAGMA
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    PRAGMA journal_mode=WAL;
                    PRAGMA foreign_keys=ON;
                    PRAGMA synchronous=NORMAL;
                ";
                command.ExecuteNonQuery();
            }

            DatabaseMigrationRunner.Run(connection);

            // 資料清理 (依據保留天數)
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    DELETE FROM ContextLogs WHERE TriggerEventId IN (
                        SELECT Id FROM Events WHERE Timestamp < datetime('now', @days));
                    DELETE FROM AnalysisResults WHERE EventId IN (
                        SELECT Id FROM Events WHERE Timestamp < datetime('now', @days));
                    DELETE FROM Events WHERE Timestamp < datetime('now', @days);
                ";
                command.Parameters.AddWithValue("@days", $"-{retentionDays} days");
                command.ExecuteNonQuery();
            }
        }

        public async Task<long> SaveEventAsync(EventRecord evt, CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            // 使用 ON CONFLICT(EventId, Source, Timestamp) DO UPDATE SET Id=Id RETURNING Id
            // 確保無論是新插入還是因重複而被忽略，都能獲取該筆記錄的資料庫 Id
            command.CommandText = @"
                INSERT INTO Events (Source, Level, EventId, ProviderName, Message, Timestamp, Host)
                VALUES (@source, @level, @eventId, @provider, @message, @timestamp, @host)
                ON CONFLICT(EventId, Source, Timestamp) DO UPDATE SET Id=Id
                RETURNING Id;
            ";
            command.Parameters.AddWithValue("@source", evt.Source);
            command.Parameters.AddWithValue("@level", (int)evt.Level);
            command.Parameters.AddWithValue("@eventId", evt.EventId);
            command.Parameters.AddWithValue("@provider", (object?)evt.ProviderName ?? DBNull.Value);
            command.Parameters.AddWithValue("@message", (object?)evt.Message ?? DBNull.Value);
            command.Parameters.AddWithValue("@timestamp", evt.Timestamp);
            command.Parameters.AddWithValue("@host", evt.Host);

            var result = await command.ExecuteScalarAsync(ct);
            return result != null ? Convert.ToInt64(result) : 0;
        }

        public async Task<IReadOnlyList<EventRecord>> GetRecentEventsAsync(int limit = 200, CancellationToken ct = default)
        {
            var results = new List<EventRecord>();
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Events ORDER BY Timestamp DESC LIMIT @limit";
            command.Parameters.AddWithValue("@limit", limit);

            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new EventRecord
                {
                    Id = reader.GetInt32(0),
                    Source = reader.GetString(1),
                    Level = (EventLevel)reader.GetInt32(2),
                    EventId = reader.GetInt32(3),
                    ProviderName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Message = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Timestamp = reader.GetDateTime(6),
                    IsAnalyzed = reader.GetInt32(7) == 1,
                    Host = reader.GetString(8),
                    CreatedAt = reader.GetDateTime(9)
                });
            }
            return results;
        }

        public async Task MarkEventAnalyzedAsync(long eventId, CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Events SET IsAnalyzed = 1 WHERE Id = @id";
            command.Parameters.AddWithValue("@id", eventId);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task ClearAllLogsAsync(CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (string sql in new[]
                {
                    "DELETE FROM ContextLogs;",
                    "DELETE FROM AnalysisResults;",
                    "DELETE FROM Events;"
                })
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(ct);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        
        public async Task<IReadOnlyList<EventRecord>> GetContextLogsAsync(long triggerEventId, CancellationToken ct = default)
        {
            var results = new List<EventRecord>();
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Source, Level, EventId, Message, Timestamp FROM ContextLogs WHERE TriggerEventId = @triggerEventId ORDER BY Timestamp ASC";
            command.Parameters.AddWithValue("@triggerEventId", triggerEventId);

            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new EventRecord
                {
                    // Id here refers to ContextLogs.Id, not Events.Id
                    Id = reader.GetInt32(0), 
                    Source = reader.GetString(1),
                    Level = (EventLevel)reader.GetInt32(2),
                    EventId = reader.GetInt32(3),
                    Message = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Timestamp = reader.GetDateTime(5)
                    // Note: ProviderName, IsAnalyzed, Host, CreatedAt are not in ContextLogs table
                });
            }
            return results;
        }
        
        public async Task<bool> HasContextLogsAsync(long triggerEventId, CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM ContextLogs WHERE TriggerEventId = @id LIMIT 1";
            command.Parameters.AddWithValue("@id", triggerEventId);
            var result = await command.ExecuteScalarAsync(ct);
            return result != null;
        }

        public async Task DeleteContextLogsAsync(long triggerEventId, CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ContextLogs WHERE TriggerEventId = @id";
            command.Parameters.AddWithValue("@id", triggerEventId);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task SaveContextLogsAsync(IEnumerable<EventRecord> contextLogs, long triggerEventId, CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var transaction = connection.BeginTransaction();
            foreach (var log in contextLogs)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO ContextLogs (TriggerEventId, Source, Level, EventId, Message, Timestamp)
                    VALUES (@triggerEventId, @source, @level, @eventId, @message, @timestamp);
                ";
                command.Parameters.AddWithValue("@triggerEventId", triggerEventId);
                command.Parameters.AddWithValue("@source", log.Source);
                command.Parameters.AddWithValue("@level", (int)log.Level);
                command.Parameters.AddWithValue("@eventId", log.EventId);
                command.Parameters.AddWithValue("@message", (object?)log.Message ?? DBNull.Value);
                command.Parameters.AddWithValue("@timestamp", log.Timestamp);
                await command.ExecuteNonQueryAsync(ct);
            }
            transaction.Commit();
        }

        public async Task<long> SaveAnalysisResultAsync(AnalysisResult result, CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO AnalysisResults
                (EventId, AiModel, ModelName, Prompt, Response, IsSuccess, ErrorMessage)
                VALUES (@eventId, @aiModel, @modelName, @prompt, @response, @isSuccess, @errorMessage);
                SELECT last_insert_rowid();
            ";
            command.Parameters.AddWithValue("@eventId", result.EventId);
            command.Parameters.AddWithValue("@aiModel", result.AiModel);
            command.Parameters.AddWithValue("@modelName", (object?)result.ModelName ?? DBNull.Value);
            command.Parameters.AddWithValue("@prompt", (object?)result.Prompt ?? DBNull.Value);
            command.Parameters.AddWithValue("@response", (object?)result.Response ?? DBNull.Value);
            command.Parameters.AddWithValue("@isSuccess", result.IsSuccess ? 1 : 0);
            command.Parameters.AddWithValue("@errorMessage", (object?)result.ErrorMessage ?? DBNull.Value);

            var scalar = await command.ExecuteScalarAsync(ct);
            return scalar != null ? Convert.ToInt64(scalar) : 0;
        }

        public async Task<AnalysisResult?> GetLatestAnalysisAsync(long eventId, CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, EventId, AiModel, ModelName, Prompt, Response, IsSuccess, ErrorMessage, CreatedAt
                FROM AnalysisResults
                WHERE EventId = @eventId
                ORDER BY CreatedAt DESC, Id DESC
                LIMIT 1;
            ";
            command.Parameters.AddWithValue("@eventId", eventId);

            using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new AnalysisResult
                {
                    Id = reader.GetInt32(0),
                    EventId = reader.GetInt32(1),
                    AiModel = reader.GetString(2),
                    ModelName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Prompt = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Response = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsSuccess = reader.GetInt32(6) == 1,
                    ErrorMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                    CreatedAt = reader.GetDateTime(8)
                };
            }
            return null;
        }

        public async Task<IReadOnlyList<AnalysisReportItem>> GetAnalysisReportItemsAsync(int limit = 100, CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    ar.Id,
                    e.Id,
                    e.Source,
                    e.Level,
                    e.EventId,
                    e.ProviderName,
                    e.Message,
                    e.Timestamp,
                    e.Host,
                    ar.AiModel,
                    ar.ModelName,
                    ar.Response,
                    ar.IsSuccess,
                    ar.ErrorMessage,
                    ar.CreatedAt
                FROM AnalysisResults ar
                INNER JOIN Events e ON e.Id = ar.EventId
                ORDER BY ar.CreatedAt DESC, ar.Id DESC
                LIMIT @limit;
            ";
            command.Parameters.AddWithValue("@limit", limit);

            var items = new List<AnalysisReportItem>();
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new AnalysisReportItem
                {
                    AnalysisId = reader.GetInt32(0),
                    EventDbId = reader.GetInt32(1),
                    Source = reader.GetString(2),
                    Level = (EventLevel)reader.GetInt32(3),
                    EventId = reader.GetInt32(4),
                    ProviderName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Message = reader.IsDBNull(6) ? null : reader.GetString(6),
                    EventTimestamp = reader.GetDateTime(7),
                    Host = reader.IsDBNull(8) ? "localhost" : reader.GetString(8),
                    AiModel = reader.GetString(9),
                    ModelName = reader.IsDBNull(10) ? null : reader.GetString(10),
                    Response = reader.IsDBNull(11) ? null : reader.GetString(11),
                    IsSuccess = reader.GetInt32(12) == 1,
                    ErrorMessage = reader.IsDBNull(13) ? null : reader.GetString(13),
                    AnalysisCreatedAt = reader.GetDateTime(14)
                });
            }

            return items;
        }

        public async Task SaveSystemSnapshotAsync(SystemSnapshot snapshot, CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO SystemSnapshots 
                (OsVersion, OsBuild, ComputerName, DomainOrWorkgroup, IpAddresses, TotalRamMb, CpuName, GpuInfo, CapturedAt)
                VALUES (@osv, @osb, @cn, @dw, @ip, @ram, @cpu, @gpu, @cat)
            ";
            command.Parameters.AddWithValue("@osv", (object?)snapshot.OsVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("@osb", (object?)snapshot.OsBuild ?? DBNull.Value);
            command.Parameters.AddWithValue("@cn", (object?)snapshot.ComputerName ?? DBNull.Value);
            command.Parameters.AddWithValue("@dw", (object?)snapshot.DomainOrWorkgroup ?? DBNull.Value);
            command.Parameters.AddWithValue("@ip", (object?)snapshot.IpAddresses ?? DBNull.Value);
            command.Parameters.AddWithValue("@ram", snapshot.TotalRamMb);
            command.Parameters.AddWithValue("@cpu", (object?)snapshot.CpuName ?? DBNull.Value);
            command.Parameters.AddWithValue("@gpu", (object?)snapshot.GpuInfo ?? DBNull.Value);
            command.Parameters.AddWithValue("@cat", snapshot.CapturedAt);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<SystemSnapshot?> GetLatestSnapshotAsync(CancellationToken ct = default)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM SystemSnapshots ORDER BY CapturedAt DESC LIMIT 1";

            using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new SystemSnapshot
                {
                    Id = reader.GetInt32(0),
                    OsVersion = reader.IsDBNull(1) ? null : reader.GetString(1),
                    OsBuild = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ComputerName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DomainOrWorkgroup = reader.IsDBNull(4) ? null : reader.GetString(4),
                    IpAddresses = reader.IsDBNull(5) ? null : reader.GetString(5),
                    TotalRamMb = reader.GetInt64(6),
                    CpuName = reader.IsDBNull(7) ? null : reader.GetString(7),
                    GpuInfo = reader.IsDBNull(8) ? null : reader.GetString(8),
                    CapturedAt = reader.GetDateTime(9)
                };
            }
            return null;
        }

        public async Task SaveSecretAsync(string key, string value)
        {
            string encryptedValue = EncryptionHelper.Encrypt(value);
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO AppSettings (Key, Value, IsEncrypted)
                VALUES (@key, @value, 1)
            ";
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", encryptedValue);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<string?> GetSecretAsync(string key)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value, IsEncrypted FROM AppSettings WHERE Key = @key";
            command.Parameters.AddWithValue("@key", key);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string value = reader.GetString(0);
                int isEncrypted = reader.GetInt32(1);

                if (isEncrypted == 1)
                {
                    return EncryptionHelper.Decrypt(value);
                }
                return value;
            }
            return null;
        }

        public async Task DeleteSecretAsync(string key)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM AppSettings WHERE Key = @key";
            command.Parameters.AddWithValue("@key", key);
            await command.ExecuteNonQueryAsync();
        }
    }
}
