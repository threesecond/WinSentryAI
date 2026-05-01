namespace WinSentryAI.Tests;

internal static class TestPaths
{
    public static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "WinSentryAI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
