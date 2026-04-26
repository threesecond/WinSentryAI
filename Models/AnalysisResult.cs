using System;

namespace WinSentryAI.Models
{
    public record AnalysisResult
    {
        public int Id { get; init; }
        public int EventId { get; init; }
        public string AiModel { get; init; } = string.Empty;
        public string? ModelName { get; init; }
        public string? Prompt { get; init; }
        public string? Response { get; init; }
        public bool IsSuccess { get; init; } = true;
        public string? ErrorMessage { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
