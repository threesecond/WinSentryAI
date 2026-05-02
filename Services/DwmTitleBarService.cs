using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Serilog;

namespace WinSentryAI.Services
{
    internal static class DwmTitleBarService
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        public static void Apply(Window window, string effectiveTheme)
        {
            if (window == null)
                return;

            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero)
                return;

            bool isDark = string.Equals(effectiveTheme, "Dark", StringComparison.OrdinalIgnoreCase);

            try
            {
                SetBooleanAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, isDark);
                SetBooleanAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, isDark);

                var caption = isDark ? Color.FromRgb(0x0F, 0x17, 0x2A) : Color.FromRgb(0xF8, 0xFA, 0xFC);
                var border = isDark ? Color.FromRgb(0x33, 0x41, 0x55) : Color.FromRgb(0xDD, 0xE3, 0xEA);
                var text = isDark ? Color.FromRgb(0xF9, 0xFA, 0xFB) : Color.FromRgb(0x11, 0x18, 0x27);

                SetColorAttribute(helper.Handle, DWMWA_CAPTION_COLOR, caption);
                SetColorAttribute(helper.Handle, DWMWA_BORDER_COLOR, border);
                SetColorAttribute(helper.Handle, DWMWA_TEXT_COLOR, text);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "DWM title bar styling is not available on this Windows version.");
            }
        }

        private static void SetBooleanAttribute(IntPtr hwnd, int attribute, bool enabled)
        {
            int value = enabled ? 1 : 0;
            _ = DwmSetWindowAttribute(hwnd, attribute, ref value, Marshal.SizeOf<int>());
        }

        private static void SetColorAttribute(IntPtr hwnd, int attribute, Color color)
        {
            int colorRef = color.R | (color.G << 8) | (color.B << 16);
            _ = DwmSetWindowAttribute(hwnd, attribute, ref colorRef, Marshal.SizeOf<int>());
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);
    }
}
