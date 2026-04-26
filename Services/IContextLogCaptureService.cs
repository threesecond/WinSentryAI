using System.Threading;
using System.Threading.Tasks;
using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    /// <summary>
    /// 將 trigger event 前後 ±1 分鐘的相關事件擷取並寫入 ContextLogs 表。
    /// 提供兩條獨立路徑：
    ///   - EnsureContextLogsAsync：retrospective / on-demand backfill，立即執行（時間窗已完整）
    ///   - ScheduleWatcherCapture：watcher 路徑，延後到 trigger.Timestamp + 60s 後才查並重建結果
    /// </summary>
    public interface IContextLogCaptureService
    {
        /// <summary>
        /// 立即擷取（用於 retrospective 與 on-demand backfill）。
        /// 若事件太新（&lt; 60 秒）會 no-op，避免寫入殘缺結果，等待 watcher 延後 capture 處理。
        /// 對歷史事件採 idempotent：DB 已有資料 → 直接返回。
        /// </summary>
        Task EnsureContextLogsAsync(EventRecord trigger, long triggerDbId, CancellationToken ct = default);

        /// <summary>
        /// 排程 watcher 事件的延後 capture：
        /// 等待到 trigger.Timestamp + 60s 後背景執行單次完整 capture，
        /// 並 delete + insert 重建該 trigger 的 ContextLogs，覆蓋任何先前殘缺結果。
        /// 不阻塞呼叫端。
        /// </summary>
        void ScheduleWatcherCapture(EventRecord trigger, long triggerDbId);
    }
}
