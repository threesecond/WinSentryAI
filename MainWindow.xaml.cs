using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinSentryAI.Models;
using WinSentryAI.Services;
using WinSentryAI.ViewModels;

namespace WinSentryAI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            DataContextChanged += (_, _) => SubscribeToViewModel();
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            string theme = AppState.Instance.Settings?.Get("UI", "Theme", "System") ?? "System";
            ThemeService.ApplyTitleBar(this, theme);
        }

        private void SubscribeToViewModel()
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                vm.ShowConnectDialogAsync = ShowConnectDialog;
            }
        }

        private Task<(RemoteEventLogService? service, int queryHours)> ShowConnectDialog()
        {
            var vm = new RemoteConnectionViewModel(AppState.Instance.Settings);
            var dialog = new RemoteConnectionWindow { DataContext = vm, Owner = this };
            bool? result = dialog.ShowDialog();
            if (result == true && vm.ConnectedService != null)
                return Task.FromResult<(RemoteEventLogService?, int)>((vm.ConnectedService, vm.QueryHours));
            return Task.FromResult<(RemoteEventLogService?, int)>((null, 0));
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsEventListActive) && sender is MainViewModel vm)
                ApplyDetailPanelVisibility(vm.IsEventListActive);
        }

        private void ApplyDetailPanelVisibility(bool isEventList)
        {
            if (isEventList)
            {
                SplitterRow.Height = new GridLength(4);
                DetailRow.Height = new GridLength(300);
                DetailRow.MinHeight = 150;
            }
            else
            {
                SplitterRow.Height = new GridLength(0);
                DetailRow.Height = new GridLength(0);
                DetailRow.MinHeight = 0;
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.LoadEventsCommand.ExecuteAsync(null);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (Application.Current is App { IsShuttingDown: true })
                return;

            e.Cancel = true;

            var settings = AppState.Instance.Settings;
            if (!settings.GetBool("UI", "TrayHintShown", false))
            {
                var hint = new TrayHintWindow { Owner = this };
                hint.ShowDialog();
                if (hint.DontShowAgain)
                {
                    settings.SetBool("UI", "TrayHintShown", true);
                    settings.Save();
                }
            }

            Hide();
        }
    }
}
