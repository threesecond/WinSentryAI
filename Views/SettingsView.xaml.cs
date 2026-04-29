using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using WinSentryAI.ViewModels;

namespace WinSentryAI.Views
{
    public partial class SettingsView : UserControl
    {
        private const string ApiKeyMask = "********";

        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
            DataContextChanged += SettingsView_DataContextChanged;
        }

        private void SaveGeminiKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            string pwd = GeminiKeyPasswordBox.Password;
            if (IsMask(pwd)) return;
            if (vm.SaveGeminiKeyCommand.CanExecute(pwd))
            {
                vm.SaveGeminiKeyCommand.Execute(pwd);
                GeminiKeyPasswordBox.Password = ApiKeyMask;
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
            if (IsMask(pwd)) return;
            if (vm.SaveOpenAiKeyCommand.CanExecute(pwd))
            {
                vm.SaveOpenAiKeyCommand.Execute(pwd);
                OpenAiKeyPasswordBox.Password = ApiKeyMask;
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
            if (IsMask(pwd)) return;
            if (vm.SaveClaudeKeyCommand.CanExecute(pwd))
            {
                vm.SaveClaudeKeyCommand.Execute(pwd);
                ClaudeKeyPasswordBox.Password = ApiKeyMask;
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

        private void SettingsView_Loaded(object sender, RoutedEventArgs e) => RefreshApiKeyMasks();

        private void SettingsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SettingsViewModel oldVm)
                oldVm.PropertyChanged -= SettingsViewModel_PropertyChanged;
            if (e.NewValue is SettingsViewModel newVm)
                newVm.PropertyChanged += SettingsViewModel_PropertyChanged;

            RefreshApiKeyMasks();
        }

        private void SettingsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SettingsViewModel.IsGeminiKeySet)
                or nameof(SettingsViewModel.IsOpenAiKeySet)
                or nameof(SettingsViewModel.IsClaudeKeySet))
            {
                RefreshApiKeyMasks();
            }
        }

        private void RefreshApiKeyMasks()
        {
            if (DataContext is not SettingsViewModel vm) return;

            SetMaskIfNeeded(GeminiKeyPasswordBox, vm.IsGeminiKeySet);
            SetMaskIfNeeded(OpenAiKeyPasswordBox, vm.IsOpenAiKeySet);
            SetMaskIfNeeded(ClaudeKeyPasswordBox, vm.IsClaudeKeySet);
        }

        private static void SetMaskIfNeeded(PasswordBox passwordBox, bool isKeySet)
        {
            if (isKeySet)
            {
                if (string.IsNullOrEmpty(passwordBox.Password))
                    passwordBox.Password = ApiKeyMask;
            }
            else if (IsMask(passwordBox.Password))
            {
                passwordBox.Clear();
            }
        }

        private void ApiKeyPasswordBox_GotKeyboardFocus(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox && IsMask(passwordBox.Password))
                passwordBox.Clear();
        }

        private static bool IsMask(string value) => value == ApiKeyMask;
    }
}
