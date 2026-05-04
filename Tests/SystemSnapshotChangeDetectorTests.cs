using WinSentryAI.Models;
using WinSentryAI.Services;

namespace WinSentryAI.Tests;

public class SystemSnapshotChangeDetectorTests
{
    [Fact]
    public void IsLikelyDifferentComputer_returns_false_when_no_previous_snapshot_exists()
    {
        var current = Snapshot("CURRENT-PC", "CPU A", 16384);

        Assert.False(SystemSnapshotChangeDetector.IsLikelyDifferentComputer(null, current));
    }

    [Fact]
    public void IsLikelyDifferentComputer_detects_computer_name_change()
    {
        var previous = Snapshot("OLD-PC", "CPU A", 16384);
        var current = Snapshot("NEW-PC", "CPU A", 16384);

        Assert.True(SystemSnapshotChangeDetector.IsLikelyDifferentComputer(previous, current));
    }

    [Fact]
    public void IsLikelyDifferentComputer_ignores_case_only_computer_name_change()
    {
        var previous = Snapshot("OPS-LAPTOP", "CPU A", 16384);
        var current = Snapshot("ops-laptop", "CPU A", 16384);

        Assert.False(SystemSnapshotChangeDetector.IsLikelyDifferentComputer(previous, current));
    }

    [Fact]
    public void IsLikelyDifferentComputer_uses_cpu_and_ram_as_secondary_signal()
    {
        var previous = Snapshot("SAME-NAME", "CPU A", 8192);
        var current = Snapshot("SAME-NAME", "CPU B", 32768);

        Assert.True(SystemSnapshotChangeDetector.IsLikelyDifferentComputer(previous, current));
    }

    [Fact]
    public void IsLikelyDifferentComputer_does_not_prompt_for_ram_only_change()
    {
        var previous = Snapshot("SAME-PC", "CPU A", 8192);
        var current = Snapshot("SAME-PC", "CPU A", 16384);

        Assert.False(SystemSnapshotChangeDetector.IsLikelyDifferentComputer(previous, current));
    }

    private static SystemSnapshot Snapshot(string computerName, string cpuName, long totalRamMb) =>
        new()
        {
            ComputerName = computerName,
            CpuName = cpuName,
            TotalRamMb = totalRamMb,
            CapturedAt = DateTime.Now
        };
}
