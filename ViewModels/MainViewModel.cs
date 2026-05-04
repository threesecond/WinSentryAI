using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WinSentryAI.Models;
using WinSentryAI.Services;

namespace WinSentryAI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // View-related properties for EventList
        public ObservableCollection<EventRecord> Events { get; } = new();
        public ICollectionView FilteredEvents { get; }

        [ObservableProperty]
        private string _filterLevel = "All";

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private EventRecord? _selectedEvent;

        [ObservableProperty]
        private bool _isEventListActive = true;

        [ObservableProperty]
        private string _currentPage = "EventList";

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty] private bool _isRemoteMode;
        [ObservableProperty] private string _remoteHost = string.Empty;

        public bool IsNonAdminMode => !AppState.Instance.IsAdministrator;

        public IAsyncRelayCommand LoadEventsCommand { get; }
        public IAsyncRelayCommand ConnectRemoteCommand { get; }
        public IRelayCommand DisconnectRemoteCommand { get; }

        // Injected from MainWindow code-behind to keep ViewModel free of View types
        public Func<Task<(RemoteEventLogService? service, int queryHours)>>? ShowConnectDialogAsync { get; set; }

        private readonly IEventLogService _localEventLogService;

        // Navigation-related properties
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        // Sub-viewmodels
        private readonly SettingsViewModel _settingsViewModel;
        private readonly SystemInfoViewModel _systemInfoViewModel;
        private readonly AIReportViewModel _aiReportViewModel;
        [ObservableProperty]
        private EventDetailViewModel _eventDetailViewModel;

        private readonly ISettingsService _settingsService;
        private readonly IDatabaseService _databaseService;

        public IRelayCommand NavigateCommand { get; }

        public MainViewModel(ISettingsService settingsService, IDatabaseService databaseService)
        {
            _settingsService = settingsService;
            _databaseService = databaseService;
            _localEventLogService = AppState.Instance.EventLog;

            // Initialize sub-viewmodels
            _settingsViewModel = new SettingsViewModel(settingsService);
            _systemInfoViewModel = new SystemInfoViewModel(databaseService);
            _aiReportViewModel = new AIReportViewModel(databaseService);
            _eventDetailViewModel = new EventDetailViewModel(
                databaseService,
                AppState.Instance.ContextLogCapture,
                AppState.Instance.AI);

            // Set initial view to Event List (this ViewModel)
            _currentViewModel = this;

            // Initialize filter view
            FilteredEvents = CollectionViewSource.GetDefaultView(Events);
            FilteredEvents.Filter = FilterEvent;

            // Initialize commands
            LoadEventsCommand = new AsyncRelayCommand(async () => await InitializeAsync());
            NavigateCommand = new RelayCommand<string>(Navigate);
            ConnectRemoteCommand = new AsyncRelayCommand(ConnectRemoteAsync);
            DisconnectRemoteCommand = new RelayCommand(DisconnectRemote);
        }

        partial void OnSelectedEventChanged(EventRecord? value)
        {
            EventDetailViewModel.SelectedEvent = value;
        }

        partial void OnFilterLevelChanged(string value) => FilteredEvents.Refresh();
        partial void OnSearchTextChanged(string value) => FilteredEvents.Refresh();

        private bool FilterEvent(object obj)
        {
            if (obj is not EventRecord evt) return false;

            if (FilterLevel != "All" && evt.Level.ToString() != FilterLevel)
                return false;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var q = SearchText;
                if (!(evt.Source.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                      (evt.Message?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                      (evt.ProviderName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true)))
                    return false;
            }

            return true;
        }

        private void Navigate(string? pageName)
        {
            switch (pageName)
            {
                case "EventList":
                    CurrentViewModel = this;
                    IsEventListActive = true;
                    CurrentPage = "EventList";
                    break;
                case "SystemInfo":
                    CurrentViewModel = _systemInfoViewModel;
                    IsEventListActive = false;
                    CurrentPage = "SystemInfo";
                    break;
                case "AIReport":
                    CurrentViewModel = _aiReportViewModel;
                    IsEventListActive = false;
                    CurrentPage = "AIReport";
                    _ = _aiReportViewModel.LoadAsync();
                    break;
                case "Settings":
                    CurrentViewModel = _settingsViewModel;
                    IsEventListActive = false;
                    CurrentPage = "Settings";
                    break;
                default:
                    CurrentViewModel = this; // Fallback
                    IsEventListActive = true;
                    CurrentPage = "EventList";
                    break;
            }
        }

        public void ClearLoadedEvents()
        {
            SelectedEvent = null;
            Events.Clear();
            FilteredEvents.Refresh();
            UpdateStatus(0);
        }

        public void RefreshLocalizedText()
        {
            if (IsRemoteMode && !string.IsNullOrWhiteSpace(RemoteHost))
            {
                StatusText = string.Format(GetString("Remote_Status_Loaded"), Events.Count, RemoteHost);
                return;
            }

            UpdateStatus(Events.Count(e => e.Level <= EventLevel.Error));
        }

        private static string GetString(string key) =>
            Application.Current.FindResource(key) as string ?? key;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            StatusText = GetString("Shell_Status_Loading");
            Log.Information("MainViewModel initializing...");

            try
            {
                await CheckSystemSnapshotAsync(ct);

                // 1. 從 DB 載入既有事件
                var dbEvents = await AppState.Instance.Database.GetRecentEventsAsync(200, ct);
                Events.Clear();
                foreach (var evt in dbEvents)
                {
                    Events.Add(evt);
                }

                // 2. 獲取上次開機時間 (WMI)
                DateTime bootTime = GetLastBootUpTime();
                Log.Information("Last boot up time: {BootTime}", bootTime);

                // 3. 執行回溯查詢
                var retroEvents = await AppState.Instance.EventLog.GetRetrospectiveEventsAsync(bootTime, 50, ct);
                
                int newCount = 0;
                var newlyPersisted = new List<(EventRecord trigger, long dbId)>();
                foreach (var evt in retroEvents.OrderBy(e => e.Timestamp))
                {
                    long id = await AppState.Instance.Database.SaveEventAsync(evt, ct);
                    if (id > 0)
                    {
                        // 檢查 UI 列表是否已存在（利用 Id 判斷，或是透過去重集合）
                        if (!Events.Any(e => e.Source == evt.Source && e.EventId == evt.EventId && e.Timestamp == evt.Timestamp))
                        {
                            var withId = evt with { Id = (int)id };
                            // 插入到最前面
                            Events.Insert(0, withId);
                            newCount++;
                            newlyPersisted.Add((withId, id));
                        }
                    }
                }

                // 4. 啟動即時監控
                AppState.Instance.EventLog.StartWatching(evt =>
                {
                    _ = Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            long id = await AppState.Instance.Database.SaveEventAsync(evt);
                            if (id > 0)
                            {
                                var withId = evt with { Id = (int)id };
                                Events.Insert(0, withId);
                                UpdateStatus(Events.Count(e => e.Level <= EventLevel.Error));
                                NotifyIfHighSeverity(withId);

                                // 排程延後 capture：等到 trigger.Timestamp + 60s 才查並重建，
                                // 避免在後 1 分鐘窗口未成熟時寫入殘缺結果
                                AppState.Instance.ContextLogCapture.ScheduleWatcherCapture(withId, id);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error handling new event in UI.");
                        }
                    });
                });

                UpdateStatus(Events.Count(e => e.Level <= EventLevel.Error));

                // 5. retrospective 事件的 context logs 在背景一筆一筆抓，避免同時打 EventLog API
                if (newlyPersisted.Count > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        foreach (var (trigger, dbId) in newlyPersisted)
                        {
                            try
                            {
                                await AppState.Instance.ContextLogCapture.EnsureContextLogsAsync(trigger, dbId);
                            }
                            catch (Exception capEx)
                            {
                                Log.Warning(capEx, "Retrospective context log capture failed for {Id}", dbId);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize MainViewModel.");
                StatusText = GetString("Shell_Status_Error");
            }
        }

        private async Task CheckSystemSnapshotAsync(CancellationToken ct)
        {
            try
            {
                var previousSnapshot = await _databaseService.GetLatestSnapshotAsync(ct);
                var snapshotService = new SystemSnapshotService();
                var collectTask = snapshotService.CollectAsync(ct);
                var completedTask = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(12), ct));
                if (completedTask != collectTask)
                {
                    Log.Warning("System snapshot refresh timed out at startup; continuing with event loading.");
                    return;
                }

                var currentSnapshot = await collectTask;
                bool differentComputer = SystemSnapshotChangeDetector.IsLikelyDifferentComputer(previousSnapshot, currentSnapshot);

                await _databaseService.SaveSystemSnapshotAsync(currentSnapshot, ct);
                await _systemInfoViewModel.LoadSnapshotAsync();
                Log.Information("System snapshot refreshed. DifferentComputerDetected: {DifferentComputer}", differentComputer);

                if (differentComputer)
                {
                    await PromptForDatabaseCleanupAfterComputerChangeAsync(previousSnapshot, currentSnapshot, ct);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to refresh system snapshot at startup; continuing with event loading.");
            }
        }

        private async Task PromptForDatabaseCleanupAfterComputerChangeAsync(
            SystemSnapshot? previousSnapshot,
            SystemSnapshot currentSnapshot,
            CancellationToken ct)
        {
            string previousComputer = string.IsNullOrWhiteSpace(previousSnapshot?.ComputerName)
                ? "Unknown"
                : previousSnapshot.ComputerName!;
            string currentComputer = string.IsNullOrWhiteSpace(currentSnapshot.ComputerName)
                ? Environment.MachineName
                : currentSnapshot.ComputerName!;

            string message = string.Format(
                GetString("Startup_SystemInfoMismatch_Message"),
                previousComputer,
                currentComputer);

            var result = MessageBox.Show(
                message,
                GetString("Startup_SystemInfoMismatch_Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                Log.Information("User kept existing event database after system mismatch detection.");
                return;
            }

            try
            {
                await _databaseService.ClearAllLogsAsync(ct);
                ClearLoadedEvents();
                Log.Information("Event database cleared after system mismatch detection.");
                MessageBox.Show(
                    GetString("Startup_SystemInfoMismatch_Cleared"),
                    GetString("Startup_SystemInfoMismatch_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear event database after system mismatch detection.");
                MessageBox.Show(
                    GetString("Startup_SystemInfoMismatch_ClearFailed"),
                    GetString("Startup_SystemInfoMismatch_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateStatus(int errorCount)
        {
            StatusText = errorCount > 0
                ? string.Format(GetString("Shell_Status_Alert_Count"), errorCount)
                : GetString("Shell_Status_Normal");
            AppState.Instance.Tray?.SetAlert(errorCount > 0);
        }

        private static void NotifyIfHighSeverity(EventRecord evt)
        {
            if (evt.Level > EventLevel.Error) return;

            string title = GetString("Tray_Balloon_Title");
            string message = string.Format(
                GetString("Tray_Balloon_Message"),
                evt.Level,
                evt.Source,
                evt.EventId);
            AppState.Instance.Tray?.ShowBalloon(title, message);
        }

        private async Task ConnectRemoteAsync()
        {
            if (ShowConnectDialogAsync == null) return;
            var (service, queryHours) = await ShowConnectDialogAsync();
            if (service == null) return;

            AppState.Instance.EventLog.StopWatching();
            AppState.Instance.EventLog = service;
            IsRemoteMode = true;
            RemoteHost = service.Hostname;

            await InitializeRemoteAsync(service, queryHours);
        }

        private void DisconnectRemote()
        {
            if (AppState.Instance.EventLog is RemoteEventLogService remote)
                remote.Dispose();

            AppState.Instance.EventLog = _localEventLogService;
            IsRemoteMode = false;
            RemoteHost = string.Empty;

            _ = InitializeAsync();
        }

        private async Task InitializeRemoteAsync(RemoteEventLogService service, int queryHours)
        {
            StatusText = GetString("Shell_Status_Loading");
            ClearLoadedEvents();

            try
            {
                var since = DateTime.Now.AddHours(-queryHours);
                var events = await service.GetRetrospectiveEventsAsync(since, 200);
                var persistedEvents = new List<(EventRecord trigger, long dbId)>();

                foreach (var evt in events)
                {
                    long id = await AppState.Instance.Database.SaveEventAsync(evt);
                    if (id <= 0) continue;

                    var withId = evt with { Id = (int)id };
                    Events.Add(withId);
                    persistedEvents.Add((withId, id));
                }

                int errorCount = Events.Count(e => e.Level <= EventLevel.Error);
                StatusText = string.Format(GetString("Remote_Status_Loaded"), Events.Count, service.Hostname);
                AppState.Instance.Tray?.SetAlert(errorCount > 0);

                if (persistedEvents.Count > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        foreach (var (trigger, dbId) in persistedEvents)
                        {
                            try
                            {
                                if (await AppState.Instance.Database.HasContextLogsAsync(dbId))
                                    continue;

                                var contextLogs = await service.GetContextEventsAsync(trigger.Timestamp);
                                var related = contextLogs
                                    .Where(e => !IsSameEvent(e, trigger))
                                    .OrderBy(e => e.Timestamp)
                                    .ToList();

                                if (related.Count > 0)
                                    await AppState.Instance.Database.SaveContextLogsAsync(related, dbId);
                            }
                            catch (Exception capEx)
                            {
                                Log.Warning(capEx, "Remote context log capture failed for {Id}", dbId);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load remote events from {Host}", service.Hostname);
                StatusText = GetString("Shell_Status_Error");
            }
        }

        private static bool IsSameEvent(EventRecord candidate, EventRecord trigger)
        {
            if (candidate.EventId != trigger.EventId) return false;
            if (!string.Equals(candidate.Source, trigger.Source, StringComparison.OrdinalIgnoreCase)) return false;
            return Math.Abs((candidate.Timestamp - trigger.Timestamp).TotalMilliseconds) < 100;
        }

        private DateTime GetLastBootUpTime()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    var timeStr = obj["LastBootUpTime"]?.ToString();
                    if (timeStr != null)
                    {
                        return ManagementDateTimeConverter.ToDateTime(timeStr);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get last boot time via WMI.");
            }
            return DateTime.Now.AddDays(-1); // 失敗則預設取 24 小時內
        }
    }
}
