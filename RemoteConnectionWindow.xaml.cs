using System.Windows;
using WinSentryAI.ViewModels;

namespace WinSentryAI
{
    public partial class RemoteConnectionWindow : Window
    {
        public RemoteConnectionWindow()
        {
            InitializeComponent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not RemoteConnectionViewModel vm) return;
            vm.Password = PasswordInput.SecurePassword;
            await vm.ConnectAsync();
        }
    }
}
