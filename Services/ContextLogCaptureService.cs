using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    public class ContextLogCaptureService : IContextLogCaptureService
    {
        private readonly IEventLogService _eventLog;
        private readonly IDatabaseService _database;

        // Ensure 路徑的 session-level guard：
        // 僅抑制「DB 為空、立即 capture 也得 0 筆」的同一 trigger 重複查詢
        // （例如使用者反覆選取一個無上下文的事件）。
        // 不影響 watcher 延後路徑——watcher 走另一條 ScheduleWatcherCapture，與此 set 無關。
        private readonly HashSet<long> _ensureAttemptedEmpty = new();
        private readonly object _lock = new();

        // 後 1 分鐘等待時間，與 ±1 分鐘 context window 對齊
        private static readonly TimeSpan WatcherCaptureDelay = TimeSpan.FromSeconds(60);

        // 截斷上限（與 CLAUDE.md AI prompt 規格一致，方便後續直接重用）
        private const int MaxCriticalErrors = 15;
        private const int MaxWarnings = 10;
        private const int MaxInfos = 5;

        public ContextLogCaptureService(IEventLogService eventLog, IDatabaseService database)
        {
            _eventLog = eventLog;
            _database = database;
        }

        public async Task EnsureContextLogsAsync(EventRecord trigger, long triggerDbId, CancellationToken ct = default)
        {
            if (triggerDbId <= 0) return;

            // 1. DB 已有 → 已完成，直接返回（retrospective 與 watcher 寫入後皆視為完整）
            if (await _database.HasContextLogsAsync(triggerDbId, ct)) return;

            // 2. 事件太新（後 1 分鐘還沒過）→ 不在 Ensure 路徑寫入殘缺結果
            //    這條路徑是給 retrospective 與 on-demand backfill 用的；
            //    剛到的 watcher 事件由 ScheduleWatcherCapture 在延後時補齊
            if (DateTime.Now - trigger.Timestamp < WatcherCaptureDelay)
            {
                Log.Debug("Skipping immediate capture for fresh event {Id} (window not yet complete)", triggerDbId);
                return;
            }

            // 3. 本 session 已嘗試但結果為空 → 抑制重查
            lock (_lock)
            {
                if (_ensureAttemptedEmpty.Contains(triggerDbId)) return;
            }

            try
            {
                var truncated = await QueryAndTruncateAsync(trigger, ct);
                if (truncated.Count > 0)
                {
                    await _database.SaveContextLogsAsync(truncated, triggerDbId, ct);
                    Log.Debug("Saved {Count} context logs (immediate) for trigger {Id}", truncated.Count, triggerDbId);
                }
                else
                {
                    // 真的就是空 → 標記抑制重查
                    lock (_lock) { _ensureAttemptedEmpty.Add(triggerDbId); }
                    Log.Debug("No context logs found (immediate) for trigger {Id}", triggerDbId);
                }
            }
            catch (Exception)
            {
                // 失敗不進入抑制集合，下次仍可重試
                throw;
            }
        }

        public void ScheduleWatcherCapture(EventRecord trigger, long triggerDbId)
        {
            if (triggerDbId <= 0) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    // 等到 trigger.Timestamp + 60s 後才查，確保後 1 分鐘事件已寫入 EventLog
                    var dueAt = trigger.Timestamp + WatcherCaptureDelay;
                    var remaining = dueAt - DateTime.Now;
                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(remaining);
                    }

                    var truncated = await QueryAndTruncateAsync(trigger, CancellationToken.None);

                    // 重建：先刪舊（防禦性，覆蓋任何過早殘缺結果），再寫入完整結果
                    await _database.DeleteContextLogsAsync(triggerDbId, CancellationToken.None);
                    if (truncated.Count > 0)
                    {
                        await _database.SaveContextLogsAsync(truncated, triggerDbId, CancellationToken.None);
                        Log.Debug("Saved {Count} context logs (delayed watcher) for trigger {Id}", truncated.Count, triggerDbId);
                    }
                    else
                    {
                        Log.Debug("No context logs found (delayed watcher) for trigger {Id}", triggerDbId);
                    }

                    // watcher delayed capture 已是定論結果——若之後 Ensure 路徑碰到同一 trigger，
                    // HasContextLogsAsync 會在「有資料時」直接返回；「結果空時」交給 _ensureAttemptedEmpty
                    // 由 Ensure 路徑自行管理。這兩條路徑彼此不需要互相同步狀態。
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Delayed watcher context capture failed for {Id}", triggerDbId);
                }
            });
        }

        private async Task<List<EventRecord>> QueryAndTruncateAsync(EventRecord trigger, CancellationToken ct)
        {
            var raw = await _eventLog.GetContextEventsAsync(trigger.Timestamp, ct);
            var related = raw.Where(e => !IsSameAsTrigger(e, trigger));
            return TruncateByLevel(related, trigger.Timestamp);
        }

        private static bool IsSameAsTrigger(EventRecord candidate, EventRecord trigger)
        {
            if (candidate.EventId != trigger.EventId) return false;
            if (!string.Equals(candidate.Source, trigger.Source, StringComparison.OrdinalIgnoreCase)) return false;
            // EventLog 時間戳轉換可能有亞毫秒誤差，給 100ms 容忍
            return Math.Abs((candidate.Timestamp - trigger.Timestamp).TotalMilliseconds) < 100;
        }

        private static List<EventRecord> TruncateByLevel(IEnumerable<EventRecord> events, DateTime triggerTimestamp)
        {
            EventRecord[] all = events.ToArray();

            IEnumerable<EventRecord> Pick(Func<EventRecord, bool> predicate, int max) =>
                all.Where(predicate)
                   .OrderBy(e => Math.Abs((e.Timestamp - triggerTimestamp).TotalMilliseconds))
                   .Take(max);

            var critsErrors = Pick(e => e.Level == EventLevel.Critical || e.Level == EventLevel.Error, MaxCriticalErrors);
            var warnings = Pick(e => e.Level == EventLevel.Warning, MaxWarnings);
            var infos = Pick(e => e.Level == EventLevel.Info, MaxInfos);

            return critsErrors
                .Concat(warnings)
                .Concat(infos)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
    }
}
