using System.Drawing;
using System.Runtime.InteropServices;
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public void Initialize()
        {
            if (_icon != null) return;

            _normalIcon = CreateIcon(Color.FromArgb(0x00, 0x63, 0xB1));
            _alertIcon = CreateIcon(Color.FromArgb(0xC4, 0x2B, 0x1C));

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

        private static Icon CreateIcon(Color color)
        {
            using var bitmap = new Bitmap(32, 32);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(color);
                graphics.FillEllipse(brush, 2, 2, 28, 28);
                using var font = new Font("Segoe UI", 16, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
                using var textBrush = new SolidBrush(Color.White);
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString("W", font, textBrush, new RectangleF(0, 0, 32, 31), format);
            }

            IntPtr handle = bitmap.GetHicon();
            try
            {
                return (Icon)Icon.FromHandle(handle).Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        private static string GetString(string key) =>
            Application.Current.FindResource(key) as string ?? key;
    }
}
