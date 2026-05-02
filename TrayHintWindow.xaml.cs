using System.Windows;
using WinSentryAI.Models;
using WinSentryAI.Services;

namespace WinSentryAI
{
    public partial class TrayHintWindow : Window
    {
        public bool DontShowAgain => DontShowCheck.IsChecked == true;

        public TrayHintWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ApplyTitleBarTheme();
        }

        private void ApplyTitleBarTheme()
        {
            string theme = AppState.Instance.Settings?.Get("UI", "Theme", "System") ?? "System";
            ThemeService.ApplyTitleBar(this, theme);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
