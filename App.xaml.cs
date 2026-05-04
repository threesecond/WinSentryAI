using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using WinSentryAI.Services;
using WinSentryAI.Models;
using WinSentryAI.ViewModels;

namespace WinSentryAI
{
    public partial class App : Application
    {
        private const string AppId = "WinSentryAI.App";
        private const string SingleInstanceMutexName = "Local\\WinSentryAI.SingleInstance";
        private const string SingleInstancePipeName = "WinSentryAI.SingleInstance.ShowMainWindow";
        private Mutex? _singleInstanceMutex;
        private CancellationTokenSource? _singleInstancePipeCts;
        private int _isShowingUnhandledExceptionDialog;
        public bool ShowOnboarding { get; private set; }
        public bool IsShuttingDown { get; set; }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                NotifyExistingInstance();
                Shutdown();
                return;
            }
            StartSingleInstancePipeServer();

            // 1. 初始化 Serilog
            ConfigureLogging();
            RegisterGlobalExceptionHandlers();
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

            bool isAdministrator = IsCurrentUserAdministrator();
            AppState.Instance.IsAdministrator = isAdministrator;
            if (!isAdministrator)
            {
                MessageBox.Show(
                    "WinSentryAI is not running as administrator. System and Application logs may still be readable, but Security log access and some diagnostics can be limited.",
                    "WinSentryAI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            // 3. 檢查設定檔是否存在
            string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
            ShowOnboarding = !File.Exists(settingsPath);

            // 4. 初始化基礎服務
            var settingsService = new SettingsService();
            string lang = settingsService.Get("UI", "Language", "en");
            ApplyLanguage(lang);
            ApplyTheme(settingsService.Get("UI", "Theme", "System"));

            // 5. 初始化資料庫（含資料清理）
            var retentionDays = settingsService.GetInt("General", "LogRetentionDays", 7);
            var dbService = new DatabaseService();
            dbService.Initialize(retentionDays);

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
            AppState.Instance.Tray = new TrayService();
            AppState.Instance.Tray.Initialize();
            
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
                MainWindow = mainWin;
                mainWin.Show();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error during MainWindow startup.");
                throw;
            }
        }

        private static bool IsCurrentUserAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        internal void ReleaseSingleInstanceLock()
        {
            _singleInstancePipeCts?.Cancel();
            _singleInstancePipeCts?.Dispose();
            _singleInstancePipeCts = null;

            try
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex may already be released during a controlled restart.
            }

            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }

        private void StartSingleInstancePipeServer()
        {
            _singleInstancePipeCts = new CancellationTokenSource();
            var token = _singleInstancePipeCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using var pipe = new NamedPipeServerStream(
                            SingleInstancePipeName,
                            PipeDirection.In,
                            maxNumberOfServerInstances: 1,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                        await pipe.WaitForConnectionAsync(token);
                        await Dispatcher.InvokeAsync(ShowExistingWindow);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Single-instance pipe server failed.");
                        await Task.Delay(500, token).ContinueWith(_ => { }, TaskScheduler.Default);
                    }
                }
            }, token);
        }

        private static void NotifyExistingInstance()
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", SingleInstancePipeName, PipeDirection.Out);
                pipe.Connect(800);
                pipe.WriteByte(1);
            }
            catch
            {
                // If the existing instance is still starting up, the mutex still prevents a second instance.
            }
        }

        private void ShowExistingWindow()
        {
            Window? window = MainWindow;
            if (window == null || !window.IsLoaded)
                window = Windows.OfType<Window>().FirstOrDefault(w => w.IsLoaded);
            if (window == null) return;

            window.Show();
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
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

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled UI exception.");
            e.Handled = true;
            ShowUnhandledExceptionDialog("WinSentryAI encountered an unexpected error. The error has been written to the log file.");
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled AppDomain exception. IsTerminating: {IsTerminating}", e.IsTerminating);
            }
            else
            {
                Log.Fatal("Unhandled AppDomain exception object: {ExceptionObject}. IsTerminating: {IsTerminating}", e.ExceptionObject, e.IsTerminating);
            }

            ShowUnhandledExceptionDialog("WinSentryAI encountered a fatal error and may need to close. The error has been written to the log file.");
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unobserved task exception.");
            e.SetObserved();
            ShowUnhandledExceptionDialog("A background task failed unexpectedly. The error has been written to the log file.");
        }

        private void ShowUnhandledExceptionDialog(string message)
        {
            if (IsShuttingDown || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            if (Interlocked.Exchange(ref _isShowingUnhandledExceptionDialog, 1) == 1)
                return;

            try
            {
                if (Dispatcher.CheckAccess())
                {
                    MessageBox.Show(message, "WinSentryAI", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    Dispatcher.Invoke(() =>
                        MessageBox.Show(message, "WinSentryAI", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to show unhandled exception dialog.");
            }
            finally
            {
                Interlocked.Exchange(ref _isShowingUnhandledExceptionDialog, 0);
            }
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

        internal void ApplyTheme(string theme)
        {
            try
            {
                ThemeService.Apply(this, theme);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to apply theme: {Theme}", theme);
                if (!string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
                    ThemeService.Apply(this, "Light");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppState.Instance.Tray?.Dispose();
            ReleaseSingleInstanceLock();
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
