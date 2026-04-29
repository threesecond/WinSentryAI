using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace WinSentryAI.Services
{
    internal static class ThemeService
    {
        private const string HandySkinMarker = "HandyControl;component/Themes/Skin";

        public static void Apply(Application app, string theme)
        {
            var effectiveTheme = ResolveTheme(theme);
            ApplyHandyControlSkin(app, effectiveTheme);
            ApplyAppBrushes(app, effectiveTheme);
        }

        private static string ResolveTheme(string theme)
        {
            if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
                return "Dark";
            if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
                return "Light";

            return IsSystemDarkTheme() ? "Dark" : "Light";
        }

        private static bool IsSystemDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                return value is int i && i == 0;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyHandyControlSkin(Application app, string effectiveTheme)
        {
            var dictionaries = app.Resources.MergedDictionaries;
            var oldSkin = dictionaries.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains(HandySkinMarker, StringComparison.OrdinalIgnoreCase) == true);
            if (oldSkin != null)
                dictionaries.Remove(oldSkin);

            var skinName = effectiveTheme == "Dark" ? "SkinDark.xaml" : "SkinDefault.xaml";
            dictionaries.Insert(0, new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/HandyControl;component/Themes/{skinName}", UriKind.Absolute)
            });
        }

        private static void ApplyAppBrushes(Application app, string effectiveTheme)
        {
            if (effectiveTheme == "Dark")
            {
                SetBrush(app, "AppWindowBackgroundBrush", "#111827");
                SetBrush(app, "AppSidebarBrush", "#0F172A");
                SetBrush(app, "AppSurfaceBrush", "#1F2937");
                SetBrush(app, "AppSubtleSurfaceBrush", "#111827");
                SetBrush(app, "AppPanelBackgroundBrush", "#111827");
                SetBrush(app, "AppInputBackgroundBrush", "#0F172A");
                SetBrush(app, "AppHoverBrush", "#1E293B");
                SetBrush(app, "AppActiveBrush", "#173A5E");
                SetBrush(app, "AppActiveHoverBrush", "#1E4D78");
                SetBrush(app, "AppBorderBrush", "#374151");
                SetBrush(app, "AppPrimaryTextBrush", "#F9FAFB");
                SetBrush(app, "AppSecondaryTextBrush", "#CBD5E1");
                SetBrush(app, "AppMutedTextBrush", "#94A3B8");
                SetBrush(app, "AppStatusBarBrush", "#111827");
                SetBrush(app, "AppStatusPillBrush", "#1F2937");
                SetBrush(app, "AppStatusPillBorderBrush", "#374151");
                SetBrush(app, "AppInfoBrush", "#102A43");
                SetBrush(app, "AppInfoBorderBrush", "#1D4E89");
            }
            else
            {
                SetBrush(app, "AppWindowBackgroundBrush", "#F5F6F8");
                SetBrush(app, "AppSidebarBrush", "#F3F6FA");
                SetBrush(app, "AppSurfaceBrush", "#FFFFFF");
                SetBrush(app, "AppSubtleSurfaceBrush", "#FAFAFA");
                SetBrush(app, "AppPanelBackgroundBrush", "#F8F8F8");
                SetBrush(app, "AppInputBackgroundBrush", "#FFFFFF");
                SetBrush(app, "AppHoverBrush", "#ECEFF3");
                SetBrush(app, "AppActiveBrush", "#EAF4FD");
                SetBrush(app, "AppActiveHoverBrush", "#DDEFFD");
                SetBrush(app, "AppBorderBrush", "#E0E0E0");
                SetBrush(app, "AppPrimaryTextBrush", "#1F2937");
                SetBrush(app, "AppSecondaryTextBrush", "#4B5563");
                SetBrush(app, "AppMutedTextBrush", "#6B7280");
                SetBrush(app, "AppStatusBarBrush", "#FAFAFA");
                SetBrush(app, "AppStatusPillBrush", "#F3F4F6");
                SetBrush(app, "AppStatusPillBorderBrush", "#E5E7EB");
                SetBrush(app, "AppInfoBrush", "#EBF3FB");
                SetBrush(app, "AppInfoBorderBrush", "#D4E7F7");
            }
        }

        private static void SetBrush(Application app, string key, string color)
        {
            app.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }
}
