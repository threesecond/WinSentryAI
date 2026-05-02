using System.Windows;
using WinSentryAI.Models;
using WinSentryAI.Services;
using WinSentryAI.ViewModels;

namespace WinSentryAI
{
    public partial class RemoteConnectionWindow : Window
    {
        public RemoteConnectionWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ApplyTitleBarTheme();
        }

        private void ApplyTitleBarTheme()
        {
            string theme = AppState.Instance.Settings?.Get("UI", "Theme", "System") ?? "System";
            ThemeService.ApplyTitleBar(this, theme);
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not RemoteConnectionViewModel vm) return;
            vm.Password = PasswordInput.SecurePassword;
            await vm.ConnectAsync();
        }
    }
}
