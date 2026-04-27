using WinSentryAI.Services;

namespace WinSentryAI.Models
{
    /// <summary>
    /// 存放 App 生命週期內共享的服務實例與全域狀態
    /// </summary>
    public class AppState
    {
        public static AppState Instance { get; } = new();
        public IDatabaseService Database { get; set; } = null!;
        public ISettingsService Settings { get; set; } = null!;
        public IEventLogService EventLog { get; set; } = null!;
        public IContextLogCaptureService ContextLogCapture { get; set; } = null!;
        public IAIService AI { get; set; } = null!;
        public bool IsOnboarding { get; set; }
        public bool IsAdministrator { get; set; }
        public ITrayService? Tray { get; set; }
    }
}
