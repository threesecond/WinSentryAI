using System;

namespace WinSentryAI.Models
{
    public record AnalysisReportItem
    {
        public int AnalysisId { get; init; }
        public int EventDbId { get; init; }
        public string Source { get; init; } = string.Empty;
        public EventLevel Level { get; init; }
        public int EventId { get; init; }
        public string? ProviderName { get; init; }
        public string? Message { get; init; }
        public DateTime EventTimestamp { get; init; }
        public string Host { get; init; } = "localhost";
        public string AiModel { get; init; } = string.Empty;
        public string? ModelName { get; init; }
        public string? Response { get; init; }
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime AnalysisCreatedAt { get; init; }
    }
}
