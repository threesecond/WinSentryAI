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
        private string _statusText = string.Empty;

        public bool IsNonAdminMode => !AppState.Instance.IsAdministrator;

        public IAsyncRelayCommand LoadEventsCommand { get; }

        // Navigation-related properties
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        // Sub-viewmodels
        private readonly SettingsViewModel _settingsViewModel;
        private readonly SystemInfoViewModel _systemInfoViewModel;
        [ObservableProperty]
        private EventDetailViewModel _eventDetailViewModel;

        private readonly ISettingsService _settingsService;
        private readonly IDatabaseService _databaseService;

        public IRelayCommand NavigateCommand { get; }

        public MainViewModel(ISettingsService settingsService, IDatabaseService databaseService)
        {
            _settingsService = settingsService;
            _databaseService = databaseService;

            // Initialize sub-viewmodels
            _settingsViewModel = new SettingsViewModel(settingsService);
            _systemInfoViewModel = new SystemInfoViewModel(databaseService);
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
                    break;
                case "SystemInfo":
                    CurrentViewModel = _systemInfoViewModel;
                    IsEventListActive = false;
                    break;
                case "Settings":
                    CurrentViewModel = _settingsViewModel;
                    IsEventListActive = false;
                    break;
                // Add other cases for System Info, AI Report etc. later
                default:
                    CurrentViewModel = this; // Fallback
                    IsEventListActive = true;
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

        private static string GetString(string key) =>
            Application.Current.FindResource(key) as string ?? key;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            StatusText = GetString("Shell_Status_Loading");
            Log.Information("MainViewModel initializing...");

            try
            {
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
