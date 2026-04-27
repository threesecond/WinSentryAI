using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WinSentryAI.ViewModels;

namespace WinSentryAI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            DataContextChanged += (_, _) => SubscribeToViewModel();
        }

        private void SubscribeToViewModel()
        {
            if (DataContext is MainViewModel vm)
                vm.PropertyChanged += OnViewModelPropertyChanged;
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
            Hide();
        }
    }
}
