using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using WinSentryAI.ViewModels;

namespace WinSentryAI.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void SaveGeminiKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            string pwd = GeminiKeyPasswordBox.Password;
            if (vm.SaveGeminiKeyCommand.CanExecute(pwd))
            {
                vm.SaveGeminiKeyCommand.Execute(pwd);
                GeminiKeyPasswordBox.Clear();
            }
        }

        private void ClearGeminiKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            if (vm.ClearGeminiKeyCommand.CanExecute(null))
            {
                vm.ClearGeminiKeyCommand.Execute(null);
                GeminiKeyPasswordBox.Clear();
            }
        }

        private void SaveOpenAiKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            string pwd = OpenAiKeyPasswordBox.Password;
            if (vm.SaveOpenAiKeyCommand.CanExecute(pwd))
            {
                vm.SaveOpenAiKeyCommand.Execute(pwd);
                OpenAiKeyPasswordBox.Clear();
            }
        }

        private void ClearOpenAiKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            if (vm.ClearOpenAiKeyCommand.CanExecute(null))
            {
                vm.ClearOpenAiKeyCommand.Execute(null);
                OpenAiKeyPasswordBox.Clear();
            }
        }

        private void SaveClaudeKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            string pwd = ClaudeKeyPasswordBox.Password;
            if (vm.SaveClaudeKeyCommand.CanExecute(pwd))
            {
                vm.SaveClaudeKeyCommand.Execute(pwd);
                ClaudeKeyPasswordBox.Clear();
            }
        }

        private void ClearClaudeKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            if (vm.ClearClaudeKeyCommand.CanExecute(null))
            {
                vm.ClearClaudeKeyCommand.Execute(null);
                ClaudeKeyPasswordBox.Clear();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
