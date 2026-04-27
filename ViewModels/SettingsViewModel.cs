using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSentryAI.Models;
using WinSentryAI.Services;
using System.Windows;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;

namespace WinSentryAI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private const string GeminiSecretKey = "GeminiApiKey";
        private const string OpenAiSecretKey = "OpenAiApiKey";
        private const string ClaudeSecretKey = "ClaudeApiKey";

        private readonly ISettingsService _settingsService;
        private readonly IDatabaseService _databaseService;

        [ObservableProperty] private int _logRetentionDays;
        [ObservableProperty] private int _maxRetroQueryCount;
        [ObservableProperty] private string _aiProvider = "gemini";
        [ObservableProperty] private string _ollamaEndpoint = "";
        [ObservableProperty] private string _ollamaModel = "";
        [ObservableProperty] private int _maxRetryCount;
        [ObservableProperty] private string _uiTheme = "System";
        [ObservableProperty] private string _uiLanguage = "en";
        [ObservableProperty] private string _geminiModel = "gemini-2.0-flash";
        [ObservableProperty] private string _openAiModel = "gpt-4o";
        [ObservableProperty] private string _claudeModel = "claude-3-7-sonnet-latest";
        [ObservableProperty] private bool _enableRedaction = true;
        [ObservableProperty] private bool _needsRestart = false;
        [ObservableProperty] private string _lastRemoteHost = string.Empty;
        [ObservableProperty] private string _lastRemoteDomain = string.Empty;
        [ObservableProperty] private string _lastRemoteUsername = string.Empty;
        public bool HasLastRemoteHost => !string.IsNullOrWhiteSpace(LastRemoteHost);

        public ObservableCollection<string> GeminiModels { get; } = new();
        public ObservableCollection<string> OllamaModels { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFetchGeminiModelsRunning))]
        private bool _isFetchingGeminiModels = false;

        public bool IsFetchGeminiModelsRunning => IsFetchingGeminiModels;

        [ObservableProperty]
        private bool _isFetchingOllamaModels = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GeminiKeyStatusText))]
        private bool _isGeminiKeySet;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OpenAiKeyStatusText))]
        private bool _isOpenAiKeySet;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ClaudeKeyStatusText))]
        private bool _isClaudeKeySet;

        public string GeminiKeyStatusText => IsGeminiKeySet
            ? GetString("Settings_GeminiKey_Set")
            : GetString("Settings_GeminiKey_NotSet");

        public string OpenAiKeyStatusText => IsOpenAiKeySet
            ? GetString("Settings_OpenAiKey_Set")
            : GetString("Settings_OpenAiKey_NotSet");

        public string ClaudeKeyStatusText => IsClaudeKeySet
            ? GetString("Settings_ClaudeKey_Set")
            : GetString("Settings_ClaudeKey_NotSet");

        public ObservableCollection<string> AiProviders { get; } = new() { "gemini", "openai", "claude", "ollama" };
        public ObservableCollection<string> UiThemes { get; } = new() { "Light", "Dark", "System" };
        public ObservableCollection<string> UiLanguages { get; } = new() { "en", "zh-TW", "zh-CN" };

        public IRelayCommand LoadSettingsCommand { get; }
        public IRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand<string?> SaveGeminiKeyCommand { get; }
        public IAsyncRelayCommand ClearGeminiKeyCommand { get; }
        public IAsyncRelayCommand FetchGeminiModelsCommand { get; }
        public IAsyncRelayCommand FetchOllamaModelsCommand { get; }
        public IAsyncRelayCommand<string?> SaveOpenAiKeyCommand { get; }
        public IAsyncRelayCommand ClearOpenAiKeyCommand { get; }
        public IAsyncRelayCommand<string?> SaveClaudeKeyCommand { get; }
        public IAsyncRelayCommand ClearClaudeKeyCommand { get; }
        public IRelayCommand RestartCommand { get; }
        public IAsyncRelayCommand ClearAllLogsCommand { get; }
        public IRelayCommand ShowAboutCommand { get; }

        public SettingsViewModel(ISettingsService settingsService)
            : this(settingsService, AppState.Instance.Database) { }

        public SettingsViewModel(ISettingsService settingsService, IDatabaseService databaseService)
        {
            _settingsService = settingsService;
            _databaseService = databaseService;

            LoadSettingsCommand = new RelayCommand(LoadSettings);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            SaveGeminiKeyCommand = new AsyncRelayCommand<string?>(SaveGeminiKeyAsync);
            ClearGeminiKeyCommand = new AsyncRelayCommand(ClearGeminiKeyAsync);
            FetchGeminiModelsCommand = new AsyncRelayCommand(FetchGeminiModelsAsync);
            FetchOllamaModelsCommand = new AsyncRelayCommand(FetchOllamaModelsAsync);
            SaveOpenAiKeyCommand = new AsyncRelayCommand<string?>(SaveOpenAiKeyAsync);
            ClearOpenAiKeyCommand = new AsyncRelayCommand(ClearOpenAiKeyAsync);
            SaveClaudeKeyCommand = new AsyncRelayCommand<string?>(SaveClaudeKeyAsync);
            ClearClaudeKeyCommand = new AsyncRelayCommand(ClearClaudeKeyAsync);
            RestartCommand = new RelayCommand(Restart);
            ClearAllLogsCommand = new AsyncRelayCommand(ClearAllLogsAsync);
            ShowAboutCommand = new RelayCommand(ShowAbout);

            LoadSettings();
            _ = RefreshAllKeyStatusAsync();
        }

        partial void OnAiProviderChanged(string value)
        {
            // Only flag for restart if the value actually changed from the saved setting
            string savedProvider = _settingsService.Get("AI", "Provider", "gemini");
            NeedsRestart = value != savedProvider;
        }

        private void LoadSettings()
        {
            LogRetentionDays = _settingsService.GetInt("General", "LogRetentionDays", 7);
            MaxRetroQueryCount = _settingsService.GetInt("General", "MaxRetroQueryCount", 50);
            AiProvider = _settingsService.Get("AI", "Provider", "gemini");
            OllamaEndpoint = _settingsService.Get("AI", "OllamaEndpoint", "http://localhost:11434");
            OllamaModel = _settingsService.Get("AI", "OllamaModel", "llama3");
            GeminiModel = _settingsService.Get("AI", "GeminiModel", "gemini-2.0-flash");
            OpenAiModel = _settingsService.Get("AI", "OpenAiModel", "gpt-4o");
            ClaudeModel = _settingsService.Get("AI", "ClaudeModel", "claude-3-7-sonnet-latest");
            EnableRedaction = _settingsService.GetBool("AI", "EnableRedaction", true);
            MaxRetryCount = _settingsService.GetInt("ErrorHandling", "MaxRetryCount", 3);
            UiTheme = _settingsService.Get("UI", "Theme", "System");
            UiLanguage = _settingsService.Get("UI", "Language", "en");
            LastRemoteHost = _settingsService.Get("Remote", "LastHost", string.Empty);
            LastRemoteDomain = _settingsService.Get("Remote", "LastDomain", string.Empty);
            LastRemoteUsername = _settingsService.Get("Remote", "LastUsername", string.Empty);
            OnPropertyChanged(nameof(HasLastRemoteHost));
            NeedsRestart = false; // Reset after loading
        }

        private void SaveSettings()
        {
            _settingsService.SetInt("General", "LogRetentionDays", LogRetentionDays);
            _settingsService.SetInt("General", "MaxRetroQueryCount", MaxRetroQueryCount);
            _settingsService.Set("AI", "Provider", AiProvider);
            _settingsService.Set("AI", "OllamaEndpoint", OllamaEndpoint);
            _settingsService.Set("AI", "OllamaModel", OllamaModel);
            _settingsService.Set("AI", "GeminiModel", string.IsNullOrWhiteSpace(GeminiModel) ? "gemini-2.0-flash" : GeminiModel.Trim());
            _settingsService.Set("AI", "OpenAiModel", string.IsNullOrWhiteSpace(OpenAiModel) ? "gpt-4o" : OpenAiModel.Trim());
            _settingsService.Set("AI", "ClaudeModel", string.IsNullOrWhiteSpace(ClaudeModel) ? "claude-3-7-sonnet-latest" : ClaudeModel.Trim());
            _settingsService.SetBool("AI", "EnableRedaction", EnableRedaction);
            _settingsService.SetInt("ErrorHandling", "MaxRetryCount", MaxRetryCount);
            _settingsService.Set("UI", "Theme", UiTheme);
            _settingsService.Set("UI", "Language", UiLanguage);
            _settingsService.Save();

            // Apply language immediately
            ApplyLanguage(UiLanguage);

            MessageBox.Show(GetString("Settings_SaveSuccess"),
                            GetString("Settings_Title"),
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task SaveGeminiKeyAsync(string? apiKey)
        {
            string trimmed = (apiKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                MessageBox.Show(GetString("Settings_GeminiKey_EmptyWarning"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _databaseService.SaveSecretAsync(GeminiSecretKey, trimmed);
                IsGeminiKeySet = true;
                MessageBox.Show(GetString("Settings_GeminiKey_Saved"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to save Gemini API key.");
                MessageBox.Show(ex.Message, GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ClearGeminiKeyAsync()
        {
            try
            {
                await _databaseService.DeleteSecretAsync(GeminiSecretKey);
                IsGeminiKeySet = false;
                MessageBox.Show(GetString("Settings_GeminiKey_Cleared"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to clear Gemini API key.");
            }
        }

        private async Task FetchGeminiModelsAsync()
        {
            if (AppState.Instance.AI is not WinSentryAI.Services.GeminiAIService geminiService)
            {
                MessageBox.Show(GetString("Settings_GeminiKey_FetchFailed"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsFetchingGeminiModels = true;
            try
            {
                var models = await geminiService.FetchModelsAsync();
                if (models.Count == 0)
                {
                    MessageBox.Show(GetString("Settings_GeminiKey_FetchFailed"),
                                    GetString("Settings_Title"),
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                GeminiModels.Clear();
                foreach (var m in models) GeminiModels.Add(m);

                if (!GeminiModels.Contains(GeminiModel))
                    GeminiModel = GeminiModels[0];
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to fetch Gemini models.");
                MessageBox.Show(ex.Message, GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsFetchingGeminiModels = false;
            }
        }

        private async Task FetchOllamaModelsAsync()
        {
            IsFetchingOllamaModels = true;
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                string url = $"{OllamaEndpoint.TrimEnd('/')}/api/tags";
                using var resp = await http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Failed to fetch Ollama models: {resp.StatusCode}",
                                    GetString("Settings_Title"),
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("models", out var models))
                {
                    OllamaModels.Clear();
                    foreach (var m in models.EnumerateArray())
                    {
                        if (m.TryGetProperty("name", out var nameEl))
                        {
                            OllamaModels.Add(nameEl.GetString() ?? string.Empty);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to fetch Ollama models.");
                MessageBox.Show(ex.Message, GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsFetchingOllamaModels = false;
            }
        }

        private void Restart()
        {
            _settingsService.Set("AI", "Provider", AiProvider);
            _settingsService.Save();

            if (Application.Current is App app)
            {
                app.IsShuttingDown = true;
                app.ReleaseSingleInstanceLock();
            }

            Process.Start(Environment.ProcessPath!);
            Application.Current.Shutdown();
        }

        private async Task ClearAllLogsAsync()
        {
            var result = MessageBox.Show(
                GetString("Settings_ClearLogs_Confirm"),
                GetString("Settings_Section_Maintenance"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.OK) return;

            try
            {
                await _databaseService.ClearAllLogsAsync();
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
                    mainVm.ClearLoadedEvents();

                MessageBox.Show(
                    GetString("Settings_ClearLogs_Success"),
                    GetString("Settings_Section_Maintenance"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to clear logs.");
                MessageBox.Show(ex.Message, GetString("Settings_Section_Maintenance"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                GetString("About_Message"),
                GetString("About_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async Task SaveOpenAiKeyAsync(string? apiKey)
        {
            string trimmed = (apiKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                MessageBox.Show(GetString("Settings_OpenAiKey_EmptyWarning"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _databaseService.SaveSecretAsync(OpenAiSecretKey, trimmed);
                IsOpenAiKeySet = true;
                MessageBox.Show(GetString("Settings_OpenAiKey_Saved"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to save OpenAI API key.");
                MessageBox.Show(ex.Message, GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ClearOpenAiKeyAsync()
        {
            try
            {
                await _databaseService.DeleteSecretAsync(OpenAiSecretKey);
                IsOpenAiKeySet = false;
                MessageBox.Show(GetString("Settings_OpenAiKey_Cleared"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to clear OpenAI API key.");
            }
        }

        private async Task SaveClaudeKeyAsync(string? apiKey)
        {
            string trimmed = (apiKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                MessageBox.Show(GetString("Settings_ClaudeKey_EmptyWarning"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _databaseService.SaveSecretAsync(ClaudeSecretKey, trimmed);
                IsClaudeKeySet = true;
                MessageBox.Show(GetString("Settings_ClaudeKey_Saved"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to save Claude API key.");
                MessageBox.Show(ex.Message, GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ClearClaudeKeyAsync()
        {
            try
            {
                await _databaseService.DeleteSecretAsync(ClaudeSecretKey);
                IsClaudeKeySet = false;
                MessageBox.Show(GetString("Settings_ClaudeKey_Cleared"),
                                GetString("Settings_Title"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to clear Claude API key.");
            }
        }

        private async Task RefreshAllKeyStatusAsync()
        {
            try
            {
                var gemini = await _databaseService.GetSecretAsync(GeminiSecretKey);
                IsGeminiKeySet = !string.IsNullOrWhiteSpace(gemini);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to read Gemini API key state (likely DPAPI failure under different user).");
                IsGeminiKeySet = false;
            }

            try
            {
                var openai = await _databaseService.GetSecretAsync(OpenAiSecretKey);
                IsOpenAiKeySet = !string.IsNullOrWhiteSpace(openai);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to read OpenAI API key state.");
                IsOpenAiKeySet = false;
            }

            try
            {
                var claude = await _databaseService.GetSecretAsync(ClaudeSecretKey);
                IsClaudeKeySet = !string.IsNullOrWhiteSpace(claude);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to read Claude API key state.");
                IsClaudeKeySet = false;
            }
        }

        private void ApplyLanguage(string lang)
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            var oldLang = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));
            if (oldLang != null) merged.Remove(oldLang);

            try
            {
                string uri = $"Resources/Strings/Strings.{lang}.xaml";
                merged.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
            }
            catch (Exception ex)
            {
                if (lang != "en") ApplyLanguage("en");
                Serilog.Log.Error(ex, "Failed to apply language: {Language}", lang);
            }

            // Refresh dynamic strings that we built once
            OnPropertyChanged(nameof(GeminiKeyStatusText));
            OnPropertyChanged(nameof(OpenAiKeyStatusText));
            OnPropertyChanged(nameof(ClaudeKeyStatusText));
        }

        private static string GetString(string key) =>
            Application.Current.FindResource(key) as string ?? key;
    }
}
