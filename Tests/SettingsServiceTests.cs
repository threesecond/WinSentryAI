using WinSentryAI.Services;

namespace WinSentryAI.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void SettingsService_roundtrips_values_and_uses_parse_fallbacks()
    {
        string directory = TestPaths.CreateTempDirectory();
        string settingsPath = Path.Combine(directory, "settings.ini");

        var settings = new SettingsService(settingsPath);
        settings.Set("AI", "Provider", "ollama");
        settings.SetBool("AI", "EnableRedaction", true);
        settings.SetInt("General", "LogRetentionDays", 14);
        settings.Set("General", "BrokenInt", "abc");
        settings.Set("General", "BrokenBool", "maybe");
        settings.Save();

        var reloaded = new SettingsService(settingsPath);

        Assert.Equal("ollama", reloaded.Get("AI", "Provider"));
        Assert.True(reloaded.GetBool("AI", "EnableRedaction"));
        Assert.Equal(14, reloaded.GetInt("General", "LogRetentionDays"));
        Assert.Equal(7, reloaded.GetInt("General", "BrokenInt", 7));
        Assert.True(reloaded.GetBool("General", "BrokenBool", true));
    }
}
