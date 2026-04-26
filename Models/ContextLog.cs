using System;

namespace WinSentryAI.Models
{
    public record ContextLog
    {
        public int Id { get; init; }
        public int TriggerEventId { get; init; }
        public string Source { get; init; } = string.Empty;
        public EventLevel Level { get; init; }
        public int EventId { get; init; }
        public string? Message { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
