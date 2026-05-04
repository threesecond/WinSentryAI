using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    public static class SystemSnapshotChangeDetector
    {
        public static bool IsLikelyDifferentComputer(SystemSnapshot? previous, SystemSnapshot current)
        {
            if (previous == null)
                return false;

            if (IsDifferent(previous.ComputerName, current.ComputerName))
                return true;

            bool cpuChanged = IsDifferent(previous.CpuName, current.CpuName);
            bool ramChanged = previous.TotalRamMb > 0
                && current.TotalRamMb > 0
                && Math.Abs(previous.TotalRamMb - current.TotalRamMb) > 1024;

            return cpuChanged && ramChanged;
        }

        private static bool IsDifferent(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return !string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
