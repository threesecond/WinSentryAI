using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Eventing.Reader;
using WinSentryAI.Models;
using Serilog;
using WinApiEventRecord = System.Diagnostics.Eventing.Reader.EventRecord;
using AppEventRecord = WinSentryAI.Models.EventRecord;
using AppEventLevel = WinSentryAI.Models.EventLevel;

namespace WinSentryAI.Services
{
    public class RemoteEventLogService : IEventLogService, IDisposable
    {
        private readonly string _hostname;
        private readonly EventLogSession _session;
        private readonly string[] _logNames = { "System", "Application", "Security" };

        public string Hostname => _hostname;

        public RemoteEventLogService(string hostname, string? domain, string? username, SecureString? password)
        {
            _hostname = hostname;
            _session = (!string.IsNullOrWhiteSpace(username) && password != null)
                ? new EventLogSession(hostname, domain ?? string.Empty, username, password, SessionAuthentication.Default)
                : new EventLogSession(hostname);
        }

        public async Task<IReadOnlyList<AppEventRecord>> GetRetrospectiveEventsAsync(
            DateTime since, int maxCount, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var allEvents = new List<AppEventRecord>();
                string isoTime = since.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string filter = $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[@SystemTime >= '{isoTime}']]]";

                foreach (var logName in _logNames)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var query = new EventLogQuery(logName, PathType.LogName, filter)
                        {
                            Session = _session,
                            ReverseDirection = true
                        };
                        using var reader = new EventLogReader(query);
                        for (var raw = reader.ReadEvent(); raw != null; raw = reader.ReadEvent())
                        {
                            if (ct.IsCancellationRequested) break;
                            allEvents.Add(MapToEventRecord(raw));
                            if (allEvents.Count >= maxCount) break;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Log.Warning("Remote: access denied to {LogName} on {Host}.", logName, _hostname);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Remote: error querying {LogName} on {Host}.", logName, _hostname);
                    }
                    if (allEvents.Count >= maxCount) break;
                }

                return allEvents.OrderByDescending(e => e.Timestamp).Take(maxCount).ToList();
            }, ct);
        }

        public async Task<IReadOnlyList<AppEventRecord>> GetContextEventsAsync(
            DateTime triggerTimestamp, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var allEvents = new List<AppEventRecord>();
                string startIso = triggerTimestamp.AddMinutes(-1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string endIso = triggerTimestamp.AddMinutes(1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string filter = $"*[System[TimeCreated[@SystemTime >= '{startIso}' and @SystemTime <= '{endIso}']]]";

                foreach (var logName in _logNames)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var query = new EventLogQuery(logName, PathType.LogName, filter) { Session = _session };
                        using var reader = new EventLogReader(query);
                        for (var raw = reader.ReadEvent(); raw != null; raw = reader.ReadEvent())
                        {
                            if (ct.IsCancellationRequested) break;
                            allEvents.Add(MapToEventRecord(raw));
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Log.Warning("Remote: access denied to {LogName} context on {Host}.", logName, _hostname);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Remote: error querying {LogName} context on {Host}.", logName, _hostname);
                    }
                }
                return (IReadOnlyList<AppEventRecord>)allEvents;
            }, ct);
        }

        public void StartWatching(Action<AppEventRecord> onEventArrived)
        {
            Log.Information("RemoteEventLogService: real-time monitoring is not supported for remote connections.");
        }

        public void StopWatching() { }

        private AppEventRecord MapToEventRecord(WinApiEventRecord raw)
        {
            return new AppEventRecord
            {
                Source = raw.LogName,
                Level = (AppEventLevel)(raw.Level ?? 4),
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
            _session?.Dispose();
        }
    }
}
