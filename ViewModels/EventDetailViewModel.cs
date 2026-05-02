using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSentryAI.Models;
using WinSentryAI.Services;
using Serilog;

namespace WinSentryAI.ViewModels
{
    public record AiResponseSection(string Title, string Content)
    {
        public string DisplayTitle => CleanMarkdownText(Title);

        public string SeverityBadgeText => IsSeveritySection ? ExtractSeverityLevel(Content) : string.Empty;

        public string DisplayContent => IsSeveritySection ? ExtractSeverityDescription(Content) : CleanMarkdownText(Content);

        private bool IsSeveritySection => string.Equals(Title, "Severity", StringComparison.OrdinalIgnoreCase);

        private static string ExtractSeverityLevel(string content)
        {
            var text = CleanMarkdownText(content);
            var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? string.Empty;

            foreach (var level in new[] { "Critical", "High", "Medium", "Low" })
            {
                if (firstLine.StartsWith(level, StringComparison.OrdinalIgnoreCase))
                {
                    return level;
                }
            }

            return firstLine.Split(new[] { ' ', '-', '—', ':', '：' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? text;
        }

        private static string ExtractSeverityDescription(string content)
        {
            var text = CleanMarkdownText(content);
            var level = ExtractSeverityLevel(content);
            if (string.IsNullOrWhiteSpace(level))
            {
                return text;
            }

            var description = text;
            if (description.StartsWith(level, StringComparison.OrdinalIgnoreCase))
            {
                description = description[level.Length..].TrimStart();
            }

            return description.TrimStart('-', '—', ':', '：', ' ').Trim();
        }

        private static string CleanMarkdownText(string content)
        {
            var inCodeBlock = false;
            var lines = content.Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line =>
                {
                    if (line.StartsWith("```", StringComparison.Ordinal))
                    {
                        inCodeBlock = !inCodeBlock;
                        return false;
                    }

                    return !inCodeBlock && line != "---" && line != "***";
                })
                .Select(line => line.Replace("\t", "    "));

            return string.Join(Environment.NewLine, lines)
                .Replace("**", string.Empty)
                .Replace("__", string.Empty)
                .Trim();
        }
    }

    public enum AiAnalysisStatus
    {
        Idle,
        NoKey,
        Loading,
        Success,
        Failure
    }

    public partial class EventDetailViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly IContextLogCaptureService _contextLogCapture;
        private readonly IAIService _aiService;
        private string? _initialUserMessageForChat;

        [ObservableProperty]
        private EventRecord? _selectedEvent;

        [ObservableProperty]
        private ObservableCollection<EventRecord> _contextLogs = new();

        [ObservableProperty]
        private bool _isContextLogsLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAiIdle))]
        [NotifyPropertyChangedFor(nameof(IsAiNoKey))]
        [NotifyPropertyChangedFor(nameof(IsAiLoading))]
        [NotifyPropertyChangedFor(nameof(IsAiSuccess))]
        [NotifyPropertyChangedFor(nameof(IsAiFailure))]
        [NotifyPropertyChangedFor(nameof(HasInitialAnalysisContext))]
        private AiAnalysisStatus _aiStatus = AiAnalysisStatus.Idle;

        [ObservableProperty]
        private string? _aiResponseText;

        [ObservableProperty]
        private ObservableCollection<AiResponseSection> _aiResponseSections = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasAiErrorMessage))]
        private string? _aiErrorMessage;

        [ObservableProperty]
        private string? _aiModelName;

        [ObservableProperty]
        private ObservableCollection<ChatMessage> _chatHistory = new();

        [ObservableProperty]
        private ObservableCollection<ChatMessage> _visibleChatHistory = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasRedactionEntries))]
        private ObservableCollection<RedactionEntry> _redactionEntries = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendChatCommand))]
        private string _chatInput = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendChatCommand))]
        private bool _isChatLoading;

        /// <summary>
        /// 獨立追蹤「此事件是否已分析」，避免透過重新指定 SelectedEvent 更新而觸發 reset 循環。
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendChatCommand))]
        private bool _isEventAnalyzed;

        [ObservableProperty]
        private string _selectedTab = "Detail";

        public bool IsAiIdle    => AiStatus == AiAnalysisStatus.Idle;
        public bool IsAiNoKey   => AiStatus == AiAnalysisStatus.NoKey;
        public bool IsAiLoading => AiStatus == AiAnalysisStatus.Loading;
        public bool IsAiSuccess => AiStatus == AiAnalysisStatus.Success;
        public bool IsAiFailure => AiStatus == AiAnalysisStatus.Failure;
        public bool HasAiResponseSections => AiResponseSections.Count > 0;
        public bool HasAiErrorMessage => !string.IsNullOrWhiteSpace(AiErrorMessage);
        public bool HasRedactionEntries => RedactionEntries.Count > 0;
        public bool HasInitialAnalysisContext => IsAiSuccess && !string.IsNullOrWhiteSpace(_initialUserMessageForChat);

        public IAsyncRelayCommand LoadContextLogsCommand { get; }
        public IAsyncRelayCommand AnalyzeCommand { get; }
        public IAsyncRelayCommand SendChatCommand { get; }

        public EventDetailViewModel(
            IDatabaseService databaseService,
            IContextLogCaptureService contextLogCapture,
            IAIService aiService)
        {
            _databaseService = databaseService;
            _contextLogCapture = contextLogCapture;
            _aiService = aiService;
            LoadContextLogsCommand = new AsyncRelayCommand(async () => await LoadContextLogsAsync());
            AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, CanAnalyze);
            SendChatCommand = new AsyncRelayCommand(SendChatAsync, CanSendChat);
        }

        private bool CanAnalyze() => SelectedEvent != null && !IsAiLoading;

        private bool CanSendChat() => IsEventAnalyzed && !IsChatLoading && !string.IsNullOrWhiteSpace(ChatInput);

        partial void OnSelectedEventChanged(EventRecord? value)
        {
            ResetAiState();
            AnalyzeCommand.NotifyCanExecuteChanged();
            SendChatCommand.NotifyCanExecuteChanged();

            if (value != null)
            {
                _ = LoadContextLogsCommand.ExecuteAsync(null);
                _ = LoadExistingAnalysisAsync(value);
            }
            else
            {
                ContextLogs.Clear();
            }
        }

        partial void OnAiStatusChanged(AiAnalysisStatus value)
        {
            AnalyzeCommand.NotifyCanExecuteChanged();
            SendChatCommand.NotifyCanExecuteChanged();
        }

        private void ResetAiState()
        {
            AiStatus = AiAnalysisStatus.Idle;
            AiResponseText = null;
            AiResponseSections.Clear();
            OnPropertyChanged(nameof(HasAiResponseSections));
            AiErrorMessage = null;
            AiModelName = null;
            IsEventAnalyzed = false;
            ChatHistory.Clear();
            VisibleChatHistory.Clear();
            RedactionEntries.Clear();
            OnPropertyChanged(nameof(HasRedactionEntries));
            ChatInput = string.Empty;
            SelectedTab = "Detail";
            _initialUserMessageForChat = null;
            OnPropertyChanged(nameof(HasInitialAnalysisContext));
        }

        private async Task LoadExistingAnalysisAsync(EventRecord trigger)
        {
            if (trigger.Id == 0) return;
            try
            {
                var existing = await _databaseService.GetLatestAnalysisAsync(trigger.Id, CancellationToken.None);
                if (existing == null) return;
                if (!ReferenceEquals(SelectedEvent, trigger)) return;

                AiModelName = existing.ModelName;
                _initialUserMessageForChat = existing.Prompt;
                OnPropertyChanged(nameof(HasInitialAnalysisContext));
                if (existing.IsSuccess)
                {
                    AiResponseText = existing.Response;
                    UpdateAiResponseSections(existing.Response);
                    AiStatus = AiAnalysisStatus.Success;
                    IsEventAnalyzed = true;
                    SelectedTab = "AI";
                }
                else
                {
                    AiErrorMessage = existing.ErrorMessage;
                    AiStatus = AiAnalysisStatus.Failure;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read previous analysis for event {EventId}", trigger.Id);
            }
        }

        private async Task AnalyzeAsync()
        {
            var trigger = SelectedEvent;
            if (trigger == null || trigger.Id == 0) return;

            SelectedTab = "AI";
            AiModelName = _aiService.ModelName;

            // Pre-flight: API key check
            try
            {
                if (!await _aiService.IsConfiguredAsync())
                {
                    AiStatus = AiAnalysisStatus.NoKey;
                    AiErrorMessage = null;
                    AiResponseText = null;
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AI configuration check failed.");
                AiStatus = AiAnalysisStatus.NoKey;
                AiErrorMessage = ex is SecretDecryptionException
                    ? "The saved API key could not be decrypted by the current Windows account. Re-enter the API key in Settings -> AI."
                    : ex.Message;
                return;
            }

            AiResponseText = null;
            AiErrorMessage = null;
            AiStatus = AiAnalysisStatus.Loading;

            try
            {
                // 1. 收集 prompt 輸入資料
                var contextSnapshot = ContextLogs.ToList();
                var systemSnapshot = await _databaseService.GetLatestSnapshotAsync();
                var lang = AppState.Instance.Settings?.Get("UI", "Language", "en") ?? "en";

                var request = new AIAnalysisRequest(
                    TriggerEvent: trigger,
                    ContextLogs: contextSnapshot,
                    SystemSnapshot: systemSnapshot,
                    Language: lang);

                // 2. 呼叫 provider（永不丟例外，失敗時回傳 IsSuccess=false）
                var response = await _aiService.AnalyzeEventAsync(request);

                // 3. 持久化
                var record = new AnalysisResult
                {
                    EventId = trigger.Id,
                    AiModel = _aiService.ProviderId,
                    ModelName = response.ModelName,
                    Prompt = response.UserMessage, // 不含 system prompt（system 為固定模板）
                    Response = response.Response,
                    IsSuccess = response.IsSuccess,
                    ErrorMessage = response.ErrorMessage
                };
                try
                {
                    await _databaseService.SaveAnalysisResultAsync(record);
                    if (response.IsSuccess)
                    {
                        await _databaseService.MarkEventAnalyzedAsync(trigger.Id);
                    }
                }
                catch (Exception persistEx)
                {
                    Log.Warning(persistEx, "Failed to persist analysis result for event {EventId}", trigger.Id);
                }

                // 4. 若使用者已切換到別的事件，不要把舊結果塞回 UI
                if (!ReferenceEquals(SelectedEvent, trigger)) return;

                AiModelName = response.ModelName;
                _initialUserMessageForChat = response.UserMessage;
                OnPropertyChanged(nameof(HasInitialAnalysisContext));
                RedactionEntries.Clear();
                foreach (var item in response.RedactionMap.OrderBy(kv => kv.Value))
                {
                    RedactionEntries.Add(new RedactionEntry(item.Key, item.Value));
                }
                OnPropertyChanged(nameof(HasRedactionEntries));

                if (response.IsSuccess)
                {
                    AiResponseText = response.Response;
                    UpdateAiResponseSections(response.Response);
                    AiStatus = AiAnalysisStatus.Success;
                    IsEventAnalyzed = true;
                    SelectedTab = "AI";
                }
                else
                {
                    AiErrorMessage = response.ErrorMessage ?? "Analysis failed.";
                    AiStatus = AiAnalysisStatus.Failure;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during AI analysis for event {EventId}", trigger.Id);
                if (!ReferenceEquals(SelectedEvent, trigger)) return;
                AiErrorMessage = ex.Message;
                AiStatus = AiAnalysisStatus.Failure;
            }
        }

        private void UpdateAiResponseSections(string? response)
        {
            AiResponseSections.Clear();

            if (string.IsNullOrWhiteSpace(response))
            {
                OnPropertyChanged(nameof(HasAiResponseSections));
                return;
            }

            var sections = ParseMarkdownSections(response);
            foreach (var section in sections)
            {
                AiResponseSections.Add(section);
            }

            OnPropertyChanged(nameof(HasAiResponseSections));
        }

        private static IReadOnlyList<AiResponseSection> ParseMarkdownSections(string response)
        {
            var sections = new List<AiResponseSection>();
            string? currentTitle = null;
            var currentContent = new List<string>();

            foreach (var rawLine in response.Replace("\r\n", "\n").Split('\n'))
            {
                string line = rawLine.TrimEnd();
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    AddSection();
                    currentTitle = line[3..].Trim();
                    continue;
                }

                currentContent.Add(line);
            }

            AddSection();

            if (sections.Count == 0)
            {
                sections.Add(new AiResponseSection("Analysis", response.Trim()));
            }

            return sections;

            void AddSection()
            {
                if (string.IsNullOrWhiteSpace(currentTitle)) return;

                string content = string.Join(Environment.NewLine, currentContent).Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    sections.Add(new AiResponseSection(currentTitle, content));
                }

                currentContent.Clear();
            }
        }

        private async Task SendChatAsync()
        {
            var trigger = SelectedEvent;
            if (trigger == null || string.IsNullOrWhiteSpace(ChatInput)) return;

            string userInput = ChatInput.Trim();
            ChatInput = string.Empty;
            IsChatLoading = true;

            try
            {
                // 1. 準備 Prompt 背景
                var lang = AppState.Instance.Settings?.Get("UI", "Language", "en") ?? "en";
                string systemPrompt = PromptBuilder.BuildSystemPrompt(lang);

                // 2. 組裝對話歷史
                // 若為第一輪，先加入初始分析作為第一筆 Assistant 回應
                if (ChatHistory.Count == 0 && !string.IsNullOrEmpty(AiResponseText))
                {
                    string? initialUserMsg = _initialUserMessageForChat;
                    if (string.IsNullOrWhiteSpace(initialUserMsg))
                    {
                        initialUserMsg = PromptBuilder.BuildUserMessage(
                            trigger, ContextLogs.ToList(), await _databaseService.GetLatestSnapshotAsync());
                    }
                    ChatHistory.Add(new ChatMessage(ChatRole.User, initialUserMsg));
                    ChatHistory.Add(new ChatMessage(ChatRole.Assistant, AiResponseText));
                }

                ChatHistory.Add(new ChatMessage(ChatRole.User, userInput));
                VisibleChatHistory.Add(new ChatMessage(ChatRole.User, userInput));

                // 3. 呼叫 AI
                string reply = await _aiService.SendChatAsync(systemPrompt, ChatHistory.ToList());

                // 4. ReferenceEquals 保護
                if (!ReferenceEquals(SelectedEvent, trigger)) return;

                ChatHistory.Add(new ChatMessage(ChatRole.Assistant, reply));
                VisibleChatHistory.Add(new ChatMessage(ChatRole.Assistant, reply));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send follow-up chat for event {EventId}", trigger.Id);
                if (ReferenceEquals(SelectedEvent, trigger))
                {
                    var error = new ChatMessage(ChatRole.Assistant, $"[Error] {ex.Message}");
                    ChatHistory.Add(error);
                    VisibleChatHistory.Add(error);
                }
            }
            finally
            {
                IsChatLoading = false;
            }
        }

        private async Task LoadContextLogsAsync()
        {
            var trigger = SelectedEvent;
            if (trigger == null || trigger.Id == 0)
            {
                ContextLogs.Clear();
                return;
            }

            IsContextLogsLoading = true;
            ContextLogs.Clear();
            try
            {
                var logs = await _databaseService.GetContextLogsAsync(trigger.Id, CancellationToken.None);

                // DB 無資料 → 觸發補抓再重讀一次。capture service 自身有 session-level dedup，
                // 不會對同一個空結果 trigger 反覆查 EventLog。
                if (logs.Count == 0)
                {
                    try
                    {
                        await _contextLogCapture.EnsureContextLogsAsync(trigger, trigger.Id, CancellationToken.None);
                        logs = await _databaseService.GetContextLogsAsync(trigger.Id, CancellationToken.None);
                    }
                    catch (Exception capEx)
                    {
                        Log.Warning(capEx, "On-demand context log backfill failed for event {EventId}", trigger.Id);
                    }
                }

                // 使用者期間若已切換到別的事件，不要把舊的結果塞進去
                if (!ReferenceEquals(SelectedEvent, trigger)) return;

                foreach (var log in logs)
                {
                    ContextLogs.Add(log);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load context logs for event {EventId}", trigger.Id);
            }
            finally
            {
                IsContextLogsLoading = false;
            }
        }
    }
}
