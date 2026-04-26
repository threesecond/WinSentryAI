using System.Windows;
using WinSentryAI.ViewModels;

namespace WinSentryAI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Trigger initial data load for the EventList view
                await vm.LoadEventsCommand.ExecuteAsync(null);
            }
        }
    }
}
