using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace WinSentryAI.Services
{
    public sealed class TrayService : ITrayService
    {
        private readonly Queue<(string Title, string Message)> _pendingBalloons = new();
        private TaskbarIcon? _icon;
        private Icon? _normalIcon;
        private Icon? _alertIcon;
        private bool _isAlert;

        public void Initialize()
        {
            if (_icon != null) return;

            _normalIcon = LoadIconResource("Resources/Icons/TrayNormal.ico");
            _alertIcon = LoadIconResource("Resources/Icons/TrayAlert.ico");

            _icon = new TaskbarIcon
            {
                Icon = _normalIcon,
                ToolTipText = GetString("Tray_Tooltip_Normal"),
                ContextMenu = BuildMenu()
            };
            _icon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
        }

        public void SetAlert(bool isAlert)
        {
            _isAlert = isAlert;
            if (_icon == null) return;

            _icon.Icon = isAlert ? _alertIcon : _normalIcon;
            _icon.ToolTipText = GetString(isAlert ? "Tray_Tooltip_Alert" : "Tray_Tooltip_Normal");
        }

        public void ShowBalloon(string title, string message)
        {
            if (_icon == null) return;

            _pendingBalloons.Enqueue((title, message));
            while (_pendingBalloons.Count > 3)
                _pendingBalloons.Dequeue();

            var latest = _pendingBalloons.Last();
            _icon.ShowBalloonTip(latest.Title, latest.Message, _isAlert ? BalloonIcon.Error : BalloonIcon.Info);
        }

        public void Dispose()
        {
            _icon?.Dispose();
            _normalIcon?.Dispose();
            _alertIcon?.Dispose();
        }

        private static ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();

            var show = new MenuItem { Header = GetString("Tray_Menu_Show") };
            show.Click += (_, _) => ShowMainWindow();
            menu.Items.Add(show);

            var exit = new MenuItem { Header = GetString("Tray_Menu_Exit") };
            exit.Click += (_, _) =>
            {
                if (Application.Current is App app)
                    app.IsShuttingDown = true;
                Application.Current.Shutdown();
            };
            menu.Items.Add(exit);

            return menu;
        }

        private static void ShowMainWindow()
        {
            var window = Application.Current.MainWindow;
            if (window == null) return;

            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }

        private static Icon LoadIconResource(string relativePath)
        {
            var uri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);
            var resource = Application.GetResourceStream(uri)
                ?? throw new InvalidOperationException($"Tray icon resource not found: {relativePath}");
            using var stream = resource.Stream;
            using var icon = new Icon(stream);
            return (Icon)icon.Clone();
        }

        private static string GetString(string key) =>
            Application.Current.FindResource(key) as string ?? key;
    }
}
