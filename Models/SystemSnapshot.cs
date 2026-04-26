using System;

namespace WinSentryAI.Models
{
    public record SystemSnapshot
    {
        public int Id { get; init; }
        public string? OsVersion { get; init; }
        public string? OsBuild { get; init; }
        public string? ComputerName { get; init; }
        public string? DomainOrWorkgroup { get; init; }
        public string? IpAddresses { get; init; } // JSON array
        public long TotalRamMb { get; init; }
        public string? CpuName { get; init; }
        public string? GpuInfo { get; init; } // JSON array
        public DateTime CapturedAt { get; init; }
    }
}
