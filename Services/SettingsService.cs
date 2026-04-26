using System.IO;
using System.Text;

namespace WinSentryAI.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _filePath;
        private readonly Dictionary<string, Dictionary<string, string>> _data = new(StringComparer.OrdinalIgnoreCase);

        public SettingsService()
        {
            // 依照 Portable 原則，放在主程式目錄
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
            Load();
        }

        public string Get(string section, string key, string defaultValue = "")
        {
            if (_data.TryGetValue(section, out var sectionData) && sectionData.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }

        public int GetInt(string section, string key, int defaultValue = 0)
        {
            var val = Get(section, key, null!);
            return int.TryParse(val, out int result) ? result : defaultValue;
        }

        public bool GetBool(string section, string key, bool defaultValue = false)
        {
            var val = Get(section, key, null!);
            return bool.TryParse(val, out bool result) ? result : defaultValue;
        }

        public void Set(string section, string key, string value)
        {
            if (!_data.ContainsKey(section))
            {
                _data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            _data[section][key] = value;
        }

        public void SetInt(string section, string key, int value) => Set(section, key, value.ToString());
        
        public void SetBool(string section, string key, bool value) => Set(section, key, value.ToString().ToLower());

        public void Load()
        {
            _data.Clear();
            if (!File.Exists(_filePath))
            {
                InitializeDefaults();
                return;
            }

            string currentSection = "";
            foreach (var line in File.ReadAllLines(_filePath))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    if (!_data.ContainsKey(currentSection))
                        _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                else if (trimmedLine.Contains("="))
                {
                    int index = trimmedLine.IndexOf('=');
                    string key = trimmedLine.Substring(0, index).Trim();
                    string value = trimmedLine.Substring(index + 1).Trim();
                    
                    // 移除後面的註解
                    int commentIndex = value.IndexOf(';');
                    if (commentIndex >= 0) value = value.Substring(0, commentIndex).Trim();

                    if (!string.IsNullOrEmpty(currentSection))
                        _data[currentSection][key] = value;
                }
            }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            foreach (var section in _data)
            {
                sb.AppendLine($"[{section.Key}]");
                foreach (var kvp in section.Value)
                {
                    sb.AppendLine($"{kvp.Key}={kvp.Value}");
                }
                sb.AppendLine();
            }
            File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
        }

        private void InitializeDefaults()
        {
            void AddDefault(string section, string key, string value)
            {
                if (!_data.ContainsKey(section))
                    _data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _data[section][key] = value;
            }

            // [General]
            AddDefault("General", "LogRetentionDays", "7");
            AddDefault("General", "MaxRetroQueryCount", "50");

            // [AI]
            AddDefault("AI", "Provider", "gemini");
            AddDefault("AI", "OllamaEndpoint", "http://localhost:11434");
            AddDefault("AI", "OllamaModel", "llama3");

            // [ErrorHandling]
            AddDefault("ErrorHandling", "MaxRetryCount", "3");

            // [UI]
            AddDefault("UI", "Theme", "System");
            AddDefault("UI", "Language", "en");

            Save();
        }
    }
}
