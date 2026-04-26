using System;

namespace WinSentryAI.Models
{
    public record EventRecord
    {
        public int Id { get; init; }
        public string Source { get; init; } = string.Empty;
        public EventLevel Level { get; init; }
        public int EventId { get; init; }
        public string? ProviderName { get; init; }
        public string? Message { get; init; }
        public DateTime Timestamp { get; init; }
        public bool IsAnalyzed { get; init; }
        public string Host { get; init; } = "localhost";
        public DateTime CreatedAt { get; init; }
    }
}
