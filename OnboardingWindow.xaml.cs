using System.Windows;
using WinSentryAI.Models;
using WinSentryAI.Services;

namespace WinSentryAI
{
    public partial class OnboardingWindow : Window
    {
        public OnboardingWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ApplyTitleBarTheme();
        }

        private void ApplyTitleBarTheme()
        {
            string theme = AppState.Instance.Settings?.Get("UI", "Theme", "System") ?? "System";
            ThemeService.ApplyTitleBar(this, theme);
        }
    }
}
