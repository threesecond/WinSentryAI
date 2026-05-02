using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSentryAI.Models;
using WinSentryAI.Services;
using Serilog;

namespace WinSentryAI.ViewModels
{
    public partial class OnboardingViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly ISettingsService _settingsService;
        private readonly IAIService _aiService;
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StepIndex))]
        private int _currentStep = 1;

        public int StepIndex => CurrentStep - 1;

        [ObservableProperty] private string _selectedLanguage = "en";
        [ObservableProperty] private string _selectedProvider = "gemini";
        [ObservableProperty] private string _apiKey = string.Empty;
        [ObservableProperty] private string _ollamaEndpoint = "http://localhost:11434";

        [ObservableProperty] private bool _isTesting;
        [ObservableProperty] private bool _testPassed;
        [ObservableProperty] private string? _testResultMessage;

        [ObservableProperty] private bool _isFetchingModels;
        [ObservableProperty] private string _selectedModel = string.Empty;

        public ObservableCollection<string> AvailableModels { get; } = new();
        public bool HasAvailableModels => AvailableModels.Count > 0;

        public ObservableCollection<string> Languages { get; } = new() { "en", "zh-TW", "zh-CN" };
        public ObservableCollection<string> Providers { get; } = new() { "gemini", "openai", "claude", "ollama" };

        public IRelayCommand NextCommand { get; }
        public IRelayCommand BackCommand { get; }
        public IAsyncRelayCommand TestAndSaveCommand { get; }
        public IRelayCommand FinishCommand { get; }
        public IRelayCommand SkipCommand { get; }

        public OnboardingViewModel(IDatabaseService db, ISettingsService settings, IAIService ai)
        {
            _databaseService = db;
            _settingsService = settings;
            _aiService = ai;

            NextCommand = new RelayCommand(NextStep, CanNext);
            BackCommand = new RelayCommand(BackStep, () => CurrentStep > 1);
            TestAndSaveCommand = new AsyncRelayCommand(TestAndSaveAsync);
            FinishCommand = new RelayCommand(Finish);
            SkipCommand = new RelayCommand(SkipAiSetup);
        }

        private bool CanNext()
        {
            if (CurrentStep == 1) return !string.IsNullOrEmpty(SelectedLanguage);
            if (CurrentStep == 2) return !string.IsNullOrEmpty(SelectedProvider);
            if (CurrentStep == 3) return TestPassed && !string.IsNullOrWhiteSpace(SelectedModel);
            return true;
        }

        private void NextStep()
        {
            if (CurrentStep < 4) CurrentStep++;
            BackCommand.NotifyCanExecuteChanged();
            NextCommand.NotifyCanExecuteChanged();
        }

        private void BackStep()
        {
            if (CurrentStep > 1) CurrentStep--;
            BackCommand.NotifyCanExecuteChanged();
            NextCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedLanguageChanged(string value)
        {
            if (Application.Current is App app)
                app.ApplyLanguage(value);
        }

        partial void OnSelectedProviderChanged(string value)
        {
            // 切換 provider 時重置測試狀態與模型清單
            TestPassed = false;
            TestResultMessage = null;
            ApiKey = string.Empty;
            AvailableModels.Clear();
            SelectedModel = string.Empty;
            OnPropertyChanged(nameof(HasAvailableModels));
            NextCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedModelChanged(string value)
        {
            NextCommand.NotifyCanExecuteChanged();
        }

        private async Task TestAndSaveAsync()
        {
            IsTesting = true;
            TestPassed = false;
            TestResultMessage = null;
            AvailableModels.Clear();
            SelectedModel = string.Empty;
            OnPropertyChanged(nameof(HasAvailableModels));

            try
            {
                if (SelectedProvider == "ollama")
                {
                    string url = $"{OllamaEndpoint.TrimEnd('/')}/api/tags";
                    using var resp = await _http.GetAsync(url);
                    if (resp.IsSuccessStatusCode)
                    {
                        string body = await resp.Content.ReadAsStringAsync();
                        _settingsService.Set("AI", "OllamaEndpoint", OllamaEndpoint);
                        _settingsService.Set("AI", "Provider", "ollama");
                        _settingsService.Save();
                        TestPassed = true;
                        TestResultMessage = GetString("Onboarding_Test_Success");

                        ParseOllamaModels(body);
                    }
                    else
                    {
                        TestResultMessage = $"Ollama error: {resp.StatusCode}";
                    }
                }
                else
                {
                    string secretKey = SelectedProvider switch
                    {
                        "gemini" => "GeminiApiKey",
                        "openai" => "OpenAiApiKey",
                        "claude" => "ClaudeApiKey",
                        _ => throw new NotSupportedException()
                    };

                    await _databaseService.SaveSecretAsync(secretKey, ApiKey.Trim());

                    if (await _aiService.IsConfiguredAsync())
                    {
                        _settingsService.Set("AI", "Provider", SelectedProvider);
                        _settingsService.Save();
                        TestPassed = true;
                        TestResultMessage = GetString("Onboarding_Test_Success");

                        await FetchModelsAfterTestAsync();
                    }
                    else
                    {
                        TestResultMessage = GetString("Onboarding_Test_Failed");
                    }
                }
            }
            catch (Exception ex)
            {
                TestResultMessage = ex.Message;
                Log.Warning(ex, "Onboarding test failed");
            }
            finally
            {
                IsTesting = false;
                OnPropertyChanged(nameof(HasAvailableModels));
                NextCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task FetchModelsAfterTestAsync()
        {
            IsFetchingModels = true;
            try
            {
                switch (SelectedProvider)
                {
                    case "gemini":
                        await FetchGeminiModelsAsync();
                        break;

                    case "openai":
                        SelectedModel = "gpt-4o";
                        break;

                    case "claude":
                        SelectedModel = "claude-3-7-sonnet-latest";
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Model fetch failed during onboarding for {Provider}", SelectedProvider);
            }
            finally
            {
                IsFetchingModels = false;
                OnPropertyChanged(nameof(HasAvailableModels));
                NextCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task FetchGeminiModelsAsync()
        {
            string apiKey = ApiKey.Trim();
            if (string.IsNullOrWhiteSpace(apiKey)) return;

            string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(apiKey)}";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return;

            string body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("models", out var models)) return;

            var result = new List<string>();
            foreach (var m in models.EnumerateArray())
            {
                if (!m.TryGetProperty("name", out var nameEl)) continue;
                string name = nameEl.GetString() ?? string.Empty;

                bool supportsGenerate = false;
                if (m.TryGetProperty("supportedGenerationMethods", out var methods))
                    foreach (var method in methods.EnumerateArray())
                        if (method.GetString() == "generateContent") { supportsGenerate = true; break; }

                if (supportsGenerate && name.StartsWith("models/"))
                    result.Add(name["models/".Length..]);
            }
            result.Sort();

            AvailableModels.Clear();
            foreach (var m in result) AvailableModels.Add(m);

            SelectedModel = AvailableModels.Contains("gemini-2.0-flash")
                ? "gemini-2.0-flash"
                : AvailableModels.FirstOrDefault() ?? string.Empty;
        }

        private void ParseOllamaModels(string tagsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(tagsJson);
                if (!doc.RootElement.TryGetProperty("models", out var models)) return;

                AvailableModels.Clear();
                foreach (var m in models.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var nameEl))
                    {
                        string name = nameEl.GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(name))
                            AvailableModels.Add(name);
                    }
                }
                SelectedModel = AvailableModels.FirstOrDefault() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to parse Ollama model list");
            }
        }

        private void SkipAiSetup()
        {
            _settingsService.Set("UI", "Language", SelectedLanguage);
            _settingsService.Set("AI", "Provider", SelectedProvider);
            _settingsService.Set("General", "HasCompletedOnboarding", "true");
            _settingsService.Save();

            if (Application.Current.Windows.OfType<OnboardingWindow>().FirstOrDefault() is Window win)
            {
                win.DialogResult = true;
                win.Close();
            }
        }

        private void Finish()
        {
            _settingsService.Set("UI", "Language", SelectedLanguage);
            _settingsService.Set("General", "HasCompletedOnboarding", "true");

            // 儲存選定的模型名稱
            if (!string.IsNullOrWhiteSpace(SelectedModel))
            {
                string modelKey = SelectedProvider switch
                {
                    "gemini" => "GeminiModel",
                    "openai" => "OpenAiModel",
                    "claude" => "ClaudeModel",
                    "ollama" => "OllamaModel",
                    _ => "GeminiModel"
                };
                _settingsService.Set("AI", modelKey, SelectedModel.Trim());
            }

            _settingsService.Save();

            if (Application.Current.Windows.OfType<OnboardingWindow>().FirstOrDefault() is Window win)
            {
                win.DialogResult = true;
                win.Close();
            }
        }

        private static string GetString(string key) =>
            Application.Current.FindResource(key) as string ?? key;
    }
}
