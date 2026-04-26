namespace WinSentryAI.Services
{
    public interface ISettingsService
    {
        string Get(string section, string key, string defaultValue = "");
        int GetInt(string section, string key, int defaultValue = 0);
        bool GetBool(string section, string key, bool defaultValue = false);
        void Set(string section, string key, string value);
        void SetInt(string section, string key, int value);
        void SetBool(string section, string key, bool value);
        void Load();
        void Save();
    }
}
