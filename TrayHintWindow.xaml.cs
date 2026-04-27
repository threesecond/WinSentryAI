using System.Windows;

namespace WinSentryAI
{
    public partial class TrayHintWindow : Window
    {
        public bool DontShowAgain => DontShowCheck.IsChecked == true;

        public TrayHintWindow()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
