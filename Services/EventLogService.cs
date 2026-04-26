using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Eventing.Reader; // 僅保留用於 EventLogQuery 等類別
using System.Threading;
using System.Threading.Tasks;
using WinSentryAI.Models;
using Serilog;
using WinApiEventRecord = System.Diagnostics.Eventing.Reader.EventRecord;

namespace WinSentryAI.Services
{
    public class EventLogService : IEventLogService, IDisposable
    {
        private readonly string[] _logNames = { "System", "Application", "Security" };
        private readonly string _queryText = "*[System[(Level=1 or Level=2 or Level=3)]]";
        
        private readonly List<EventLogWatcher> _watchers = new();
        private Action<WinSentryAI.Models.EventRecord>? _onEventArrived;
        
        private CancellationTokenSource? _pollingCts;
        private int _retryCount = 0;
        private const int MaxRetries = 3;

        public async Task<IReadOnlyList<WinSentryAI.Models.EventRecord>> GetRetrospectiveEventsAsync(DateTime since, int maxCount, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var allEvents = new List<WinSentryAI.Models.EventRecord>();
                // Windows Event Log 使用 ISO 8601 格式時間進行 XPath 查詢
                string isoTime = since.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string timeFilter = $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[@SystemTime >= '{isoTime}']]]";

                foreach (var logName in _logNames)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var query = new EventLogQuery(logName, PathType.LogName, timeFilter)
                        {
                            ReverseDirection = true
                        };

                        using var reader = new EventLogReader(query);
                        for (var eventDetail = reader.ReadEvent(); eventDetail != null; eventDetail = reader.ReadEvent())
                        {
                            if (ct.IsCancellationRequested) break;
                            
                            allEvents.Add(MapToEventRecord(eventDetail));
                            if (allEvents.Count >= maxCount) break;
                        }
                    }
                    catch (UnauthorizedAccessException) when (logName == "Security")
                    {
                        Log.Warning("Access denied to {LogName} log (Non-admin mode). Skipping.", logName);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error querying {LogName} log.", logName);
                    }

                    if (allEvents.Count >= maxCount) break;
                }

                // 排序並過濾重複（若有的話），確保返回結果符合預期筆數
                return allEvents
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxCount)
                    .ToList() as IReadOnlyList<WinSentryAI.Models.EventRecord>;
            }, ct);
        }

        public async Task<IReadOnlyList<WinSentryAI.Models.EventRecord>> GetContextEventsAsync(
            DateTime triggerTimestamp, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var allEvents = new List<WinSentryAI.Models.EventRecord>();
                var startUtc = triggerTimestamp.AddMinutes(-1).ToUniversalTime();
                var endUtc = triggerTimestamp.AddMinutes(1).ToUniversalTime();
                string startIso = startUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string endIso = endUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                // 不過濾 Level，因規格要求含 Information；XPath 直接限制時間窗
                string contextQuery =
                    $"*[System[TimeCreated[@SystemTime >= '{startIso}' and @SystemTime <= '{endIso}']]]";

                foreach (var logName in _logNames)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var query = new EventLogQuery(logName, PathType.LogName, contextQuery);
                        using var reader = new EventLogReader(query);
                        for (var eventDetail = reader.ReadEvent(); eventDetail != null; eventDetail = reader.ReadEvent())
                        {
                            if (ct.IsCancellationRequested) break;
                            allEvents.Add(MapToEventRecord(eventDetail));
                        }
                    }
                    catch (UnauthorizedAccessException) when (logName == "Security")
                    {
                        Log.Warning("Access denied to {LogName} log when querying context window. Skipping.", logName);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error querying {LogName} log for context window.", logName);
                    }
                }

                return (IReadOnlyList<WinSentryAI.Models.EventRecord>)allEvents;
            }, ct);
        }

        public void StartWatching(Action<WinSentryAI.Models.EventRecord> onEventArrived)
        {
            _onEventArrived = onEventArrived;
            _retryCount = 0;
            InitializeWatchers();
        }

        private void InitializeWatchers()
        {
            StopWatching();

            bool anySuccess = false;
            foreach (var logName in _logNames)
            {
                try
                {
                    var query = new EventLogQuery(logName, PathType.LogName, _queryText);
                    var watcher = new EventLogWatcher(query);
                    watcher.EventRecordWritten += (s, e) =>
                    {
                        if (e.EventRecord != null)
                        {
                            try
                            {
                                _onEventArrived?.Invoke(MapToEventRecord(e.EventRecord));
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error in EventRecord callback.");
                            }
                        }
                    };
                    
                    watcher.Enabled = true;
                    _watchers.Add(watcher);
                    anySuccess = true;
                    Log.Debug("Subscribed to {LogName} log.", logName);
                }
                catch (UnauthorizedAccessException) when (logName == "Security")
                {
                    Log.Warning("Access denied to subscribe to {LogName} log.", logName);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to subscribe to {LogName} log.", logName);
                }
            }

            if (!anySuccess && _retryCount < MaxRetries)
            {
                _retryCount++;
                Log.Warning("Subscription failed. Retrying in background ({Count}/{Max})...", _retryCount, MaxRetries);
                
                // 指數退避重試
                int delaySeconds = (int)Math.Pow(2, _retryCount);
                Task.Delay(TimeSpan.FromSeconds(delaySeconds)).ContinueWith(_ => 
                {
                    if (_onEventArrived != null) InitializeWatchers();
                });
            }
            else if (!anySuccess)
            {
                Log.Error("All subscription attempts failed after {Max} retries. Falling back to polling mode (60s).", MaxRetries);
                StartPolling();
            }
        }

        private async void StartPolling()
        {
            _pollingCts = new CancellationTokenSource();
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            DateTime lastCheck = DateTime.Now;

            Log.Information("EventLog polling started.");

            try
            {
                while (await timer.WaitForNextTickAsync(_pollingCts.Token))
                {
                    var newEvents = await GetRetrospectiveEventsAsync(lastCheck, 50, _pollingCts.Token);
                    foreach (var evt in newEvents.OrderBy(e => e.Timestamp))
                    {
                        _onEventArrived?.Invoke(evt);
                    }
                    if (newEvents.Any())
                    {
                        lastCheck = newEvents.Max(e => e.Timestamp).AddMilliseconds(1);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Debug("EventLog polling cancelled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "EventLog polling mode error.");
            }
        }

        public void StopWatching()
        {
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
            _pollingCts = null;

            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.Enabled = false;
                    watcher.Dispose();
                }
                catch { /* Ignore dispose errors */ }
            }
            _watchers.Clear();
        }

        private WinSentryAI.Models.EventRecord MapToEventRecord(WinApiEventRecord raw)
        {
            return new WinSentryAI.Models.EventRecord
            {
                Source = raw.LogName,
                Level = (WinSentryAI.Models.EventLevel)(raw.Level ?? 4),
                EventId = raw.Id,
                ProviderName = raw.ProviderName,
                Message = raw.FormatDescription() ?? "(No Message Content)",
                Timestamp = raw.TimeCreated ?? DateTime.Now,
                Host = raw.MachineName,
                IsAnalyzed = false,
                CreatedAt = DateTime.Now
            };
        }

        public void Dispose()
        {
            StopWatching();
        }
    }
}
