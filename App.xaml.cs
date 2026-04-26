using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Serilog;
using WinSentryAI.Services;
using WinSentryAI.Models;
using WinSentryAI.ViewModels;

namespace WinSentryAI
{
    public partial class App : Application
    {
        private const string AppId = "WinSentryAI.App";
        public bool ShowOnboarding { get; private set; }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 初始化 Serilog
            ConfigureLogging();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 2. 註冊 AppUserModelId
            try
            {
                SetCurrentProcessExplicitAppUserModelID(AppId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to set AppUserModelID");
            }

            // 3. 檢查設定檔是否存在
            string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
            ShowOnboarding = !File.Exists(settingsPath);

            // 4. 初始化基礎服務
            var settingsService = new SettingsService();
            string lang = settingsService.Get("UI", "Language", "en");
            ApplyLanguage(lang);

            // 5. 初始化資料庫（含資料清理）
            var retentionDays = settingsService.GetInt("General", "LogRetentionDays", 7);
            var dbService = new DatabaseService();
            dbService.Initialize(retentionDays);

            // 6. 收集系統快照（背景執行，不等待）
            _ = Task.Run(async () =>
            {
                try
                {
                    var snapshotService = new SystemSnapshotService();
                    var snapshot = await snapshotService.CollectAsync();
                    await dbService.SaveSystemSnapshotAsync(snapshot);
                    Log.Information("System snapshot saved.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to save system snapshot at startup.");
                }
            });

            // 7. 初始化全域狀態
            var eventLogService = new EventLogService();
            AppState.Instance.Database = dbService;
            AppState.Instance.Settings = settingsService;
            AppState.Instance.EventLog = eventLogService;
            AppState.Instance.ContextLogCapture = new ContextLogCaptureService(eventLogService, dbService);

            string aiProvider = settingsService.Get("AI", "Provider", "gemini").ToLowerInvariant();
            AppState.Instance.AI = aiProvider switch
            {
                "ollama" => new OllamaAIService(settingsService),
                "openai" => new OpenAIAIService(dbService, settingsService),
                "claude" => new ClaudeAIService(dbService, settingsService),
                _ => new GeminiAIService(dbService, settingsService)
            };
            
            AppState.Instance.IsOnboarding = ShowOnboarding;

            Log.Information("WinSentryAI initialized. ShowOnboarding: {ShowOnboarding}", ShowOnboarding);

            // 8. Onboarding Wizard
            bool hasCompleted = settingsService.Get("General", "HasCompletedOnboarding", "false") == "true";
            if (!hasCompleted)
            {
                try
                {
                    var onboardingVm = new OnboardingViewModel(dbService, settingsService, AppState.Instance.AI);
                    var onboardingWin = new OnboardingWindow { DataContext = onboardingVm };
                    bool? result = onboardingWin.ShowDialog();
                    if (result != true)
                    {
                        Shutdown();
                        return;
                    }

                    // Re-instantiate AI service with the provider chosen during onboarding
                    string chosenProvider = settingsService.Get("AI", "Provider", "gemini").ToLowerInvariant();
                    AppState.Instance.AI = chosenProvider switch
                    {
                        "ollama" => new OllamaAIService(settingsService),
                        "openai" => new OpenAIAIService(dbService, settingsService),
                        "claude" => new ClaudeAIService(dbService, settingsService),
                        _ => new GeminiAIService(dbService, settingsService)
                    };
                    Log.Information("AI service re-instantiated after onboarding: {Provider}", chosenProvider);
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Fatal error during Onboarding Wizard.");
                    Shutdown();
                    return;
                }
            }

            // 9. Show MainWindow
            try
            {
                var mainVm = new MainViewModel(settingsService, dbService);
                var mainWin = new MainWindow { DataContext = mainVm };
                mainWin.Show();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error during MainWindow startup.");
                throw;
            }
        }

        private void ConfigureLogging()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "app-.log");
            
            var logConfig = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Verbose()
#else
                .MinimumLevel.Warning()
#endif
                .WriteTo.File(logPath, 
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Logger = logConfig;
        }

        internal void ApplyLanguage(string lang)
        {
            var merged = Resources.MergedDictionaries;
            var oldLang = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));
            if (oldLang != null) merged.Remove(oldLang);

            try
            {
                string uri = $"Resources/Strings/Strings.{lang}.xaml";
                merged.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
            }
            catch
            {
                if (lang != "en") ApplyLanguage("en");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
