using Microsoft.Data.Sqlite;
using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    public interface IDatabaseService
    {
        SqliteConnection GetConnection();
        void Initialize(int retentionDays = 7);
        
        // AppSettings 相關（API Keys）
        Task SaveSecretAsync(string key, string value);
        Task<string?> GetSecretAsync(string key);
        Task DeleteSecretAsync(string key);

        // 事件相關
        Task<long> SaveEventAsync(EventRecord evt, CancellationToken ct = default);
        Task<IReadOnlyList<EventRecord>> GetRecentEventsAsync(int limit = 200, CancellationToken ct = default);
        Task MarkEventAnalyzedAsync(long eventId, CancellationToken ct = default);
        Task<IReadOnlyList<EventRecord>> GetContextLogsAsync(long triggerEventId, CancellationToken ct = default);
        Task SaveContextLogsAsync(IEnumerable<EventRecord> contextLogs, long triggerEventId, CancellationToken ct = default);
        Task<bool> HasContextLogsAsync(long triggerEventId, CancellationToken ct = default);
        Task DeleteContextLogsAsync(long triggerEventId, CancellationToken ct = default);

        // 分析結果相關
        Task<long> SaveAnalysisResultAsync(AnalysisResult result, CancellationToken ct = default);
        Task<AnalysisResult?> GetLatestAnalysisAsync(long eventId, CancellationToken ct = default);

        // 系統快照相關
        Task SaveSystemSnapshotAsync(SystemSnapshot snapshot, CancellationToken ct = default);
        Task<SystemSnapshot?> GetLatestSnapshotAsync(CancellationToken ct = default);
    }
}
