using CommunityToolkit.Mvvm.ComponentModel;
using WinSentryAI.Models;
using WinSentryAI.Services;
using Serilog;
using System.Text.Json;

namespace WinSentryAI.ViewModels
{
    public partial class SystemInfoViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty] private string? _osVersion;
        [ObservableProperty] private string? _osBuild;
        [ObservableProperty] private string? _computerName;
        [ObservableProperty] private string? _domainOrWorkgroup;
        [ObservableProperty] private string? _ipAddresses;
        [ObservableProperty] private long _totalRamMb;
        [ObservableProperty] private string? _cpuName;
        [ObservableProperty] private string? _gpuInfo;

        public SystemInfoViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
            _ = LoadSnapshotAsync();
        }

        private async Task LoadSnapshotAsync()
        {
            try
            {
                var snapshot = await _databaseService.GetLatestSnapshotAsync();
                if (snapshot != null)
                {
                    OsVersion = snapshot.OsVersion;
                    OsBuild = snapshot.OsBuild;
                    ComputerName = snapshot.ComputerName;
                    DomainOrWorkgroup = snapshot.DomainOrWorkgroup;
                    TotalRamMb = snapshot.TotalRamMb;
                    CpuName = snapshot.CpuName;

                    // Format IP Addresses
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(snapshot.IpAddresses))
                        {
                            var ips = JsonSerializer.Deserialize<List<string>>(snapshot.IpAddresses);
                            IpAddresses = ips != null ? string.Join("\n", ips) : snapshot.IpAddresses;
                        }
                    }
                    catch { IpAddresses = snapshot.IpAddresses; }

                    // Format GPU Info
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(snapshot.GpuInfo))
                        {
                            using var doc = JsonDocument.Parse(snapshot.GpuInfo);
                            var gpuLines = new List<string>();
                            foreach (var element in doc.RootElement.EnumerateArray())
                            {
                                string name = element.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                                string driver = element.TryGetProperty("DriverVersion", out var d) ? d.GetString() ?? "" : "";
                                long ram = element.TryGetProperty("AdapterRamMb", out var r) ? r.GetInt64() : 0;

                                string line = name;
                                if (!string.IsNullOrEmpty(driver)) line += $" (Driver: {driver})";
                                if (ram > 0) line += $" [{ram} MB]";
                                gpuLines.Add(line);
                            }
                            GpuInfo = gpuLines.Count > 0 ? string.Join("\n", gpuLines) : snapshot.GpuInfo;
                        }
                    }
                    catch { GpuInfo = snapshot.GpuInfo; }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load latest system snapshot.");
            }
        }
    }
}
