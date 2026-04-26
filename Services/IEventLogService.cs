using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    public interface IEventLogService
    {
        /// <summary>
        /// 回溯查詢：從指定時間起到現在，篩選 Critical/Error/Warning，最多 maxCount 筆
        /// </summary>
        Task<IReadOnlyList<WinSentryAI.Models.EventRecord>> GetRetrospectiveEventsAsync(
            DateTime since, int maxCount, CancellationToken ct = default);

        /// <summary>
        /// 上下文查詢：抓取指定時間點 ±1 分鐘內所有等級事件（含 Information），供後續寫入 ContextLogs。
        /// 不過濾等級，呼叫端負責截斷 / 排序。
        /// </summary>
        Task<IReadOnlyList<WinSentryAI.Models.EventRecord>> GetContextEventsAsync(
            DateTime triggerTimestamp, CancellationToken ct = default);

        /// <summary>
        /// 即時訂閱：新事件觸發時呼叫 callback
        /// </summary>
        void StartWatching(Action<WinSentryAI.Models.EventRecord> onEventArrived);

        /// <summary>
        /// 停止訂閱並釋放資源
        /// </summary>
        void StopWatching();
    }
}
