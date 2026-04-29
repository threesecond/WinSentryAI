using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSentryAI.Models;
using WinSentryAI.Services;

namespace WinSentryAI.ViewModels
{
    public partial class AIReportViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private int _totalAnalyses;

        [ObservableProperty]
        private int _successfulAnalyses;

        [ObservableProperty]
        private int _failedAnalyses;

        [ObservableProperty]
        private int _highSeverityAnalyses;

        [ObservableProperty]
        private DateTime? _lastAnalysisAt;

        [ObservableProperty]
        private AIReportRowViewModel? _selectedItem;

        public ObservableCollection<AIReportRowViewModel> Items { get; } = new();

        public bool HasItems => Items.Count > 0;
        public bool HasSelectedItem => SelectedItem != null;

        public IAsyncRelayCommand RefreshCommand { get; }

        public AIReportViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var items = await _databaseService.GetAnalysisReportItemsAsync(100);
                Items.Clear();
                foreach (var item in items)
                {
                    Items.Add(new AIReportRowViewModel(item));
                }

                TotalAnalyses = Items.Count;
                SuccessfulAnalyses = Items.Count(i => i.IsSuccess);
                FailedAnalyses = Items.Count(i => !i.IsSuccess);
                HighSeverityAnalyses = Items.Count(i => i.IsSuccess &&
                    (string.Equals(i.Severity, "High", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(i.Severity, "Critical", StringComparison.OrdinalIgnoreCase)));
                LastAnalysisAt = Items.FirstOrDefault()?.AnalysisCreatedAt;
                SelectedItem = Items.FirstOrDefault();
                OnPropertyChanged(nameof(HasItems));
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSelectedItemChanged(AIReportRowViewModel? value) =>
            OnPropertyChanged(nameof(HasSelectedItem));
    }

    public class AIReportRowViewModel
    {
        public int AnalysisId { get; }
        public string Host { get; }
        public string Source { get; }
        public EventLevel EventLevel { get; }
        public int EventId { get; }
        public string EventTitle { get; }
        public string ModelDisplay { get; }
        public bool IsSuccess { get; }
        public string Severity { get; }
        public string Summary { get; }
        public string EventPreview { get; }
        public string DetailText { get; }
        public DateTime EventTimestamp { get; }
        public DateTime AnalysisCreatedAt { get; }

        public AIReportRowViewModel(AnalysisReportItem item)
        {
            AnalysisId = item.AnalysisId;
            Host = item.Host;
            Source = item.Source;
            EventLevel = item.Level;
            EventId = item.EventId;
            EventTitle = string.IsNullOrWhiteSpace(item.Message)
                ? item.ProviderName ?? item.Source
                : CollapseWhitespace(item.Message);
            ModelDisplay = string.IsNullOrWhiteSpace(item.ModelName) ? item.AiModel : item.ModelName!;
            IsSuccess = item.IsSuccess;
            Severity = item.IsSuccess ? ExtractSeverity(item.Response) : "Failed";
            var fullSummary = item.IsSuccess ? ExtractSummary(item.Response, 600) : item.ErrorMessage ?? "Analysis failed.";
            Summary = TrimForPreview(fullSummary, 140);
            DetailText = fullSummary;
            EventPreview = TrimForPreview(EventTitle, 150);
            EventTimestamp = item.EventTimestamp;
            AnalysisCreatedAt = item.AnalysisCreatedAt;
        }

        private static string ExtractSummary(string? response, int maxLength)
        {
            var section = ExtractSection(response, "Summary");
            if (string.IsNullOrWhiteSpace(section))
            {
                return TrimForPreview(CleanText(response), maxLength);
            }

            return TrimForPreview(CleanText(section), maxLength);
        }

        private static string ExtractSeverity(string? response)
        {
            var section = CleanText(ExtractSection(response, "Severity"));
            var firstLine = section.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? string.Empty;

            foreach (var level in new[] { "Critical", "High", "Medium", "Low" })
            {
                if (firstLine.StartsWith(level, StringComparison.OrdinalIgnoreCase))
                {
                    return level;
                }
            }

            return string.IsNullOrWhiteSpace(firstLine) ? "Unknown" : firstLine.Split(' ', '-', '—', ':', '：')[0];
        }

        private static string ExtractSection(string? response, string title)
        {
            if (string.IsNullOrWhiteSpace(response)) return string.Empty;

            string? currentTitle = null;
            var currentContent = new List<string>();
            foreach (var rawLine in response.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.TrimEnd();
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    if (string.Equals(currentTitle, title, StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Join(Environment.NewLine, currentContent).Trim();
                    }

                    currentTitle = line[3..].Trim();
                    currentContent.Clear();
                    continue;
                }

                currentContent.Add(line);
            }

            return string.Equals(currentTitle, title, StringComparison.OrdinalIgnoreCase)
                ? string.Join(Environment.NewLine, currentContent).Trim()
                : string.Empty;
        }

        private static string CleanText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            return CollapseWhitespace(value
                .Replace("**", string.Empty)
                .Replace("__", string.Empty)
                .Replace("---", string.Empty)
                .Replace("***", string.Empty));
        }

        private static string CollapseWhitespace(string value) =>
            string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static string TrimForPreview(string value, int maxLength)
        {
            if (value.Length <= maxLength) return value;
            return value[..Math.Max(0, maxLength - 1)].TrimEnd() + "…";
        }
    }
}
