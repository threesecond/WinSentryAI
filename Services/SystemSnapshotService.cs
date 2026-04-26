using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinSentryAI.Models;
using Serilog;

namespace WinSentryAI.Services
{
    public class SystemSnapshotService : ISystemSnapshotService
    {
        public async Task<SystemSnapshot> CollectAsync(CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 1. 取得 OS Build
                    string osBuild = "Unknown";
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                        osBuild = key?.GetValue("CurrentBuild")?.ToString() ?? "Unknown";
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to read OS Build from registry.");
                    }

                    // 2. 取得 Domain 或 Workgroup
                    string domain = "WORKGROUP";
                    try
                    {
                        domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
                        if (string.IsNullOrWhiteSpace(domain))
                        {
                            domain = GetWorkgroup();
                        }
                    }
                    catch { }

                    // 3. 取得 IP 位址
                    var ipList = new List<string>();
                    try
                    {
                        ipList = NetworkInterface.GetAllNetworkInterfaces()
                            .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                         ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                            .Where(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ||
                                         ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                            .Select(ua => ua.Address.ToString())
                            .ToList();
                    }
                    catch { }

                    // 4. 取得 GPU 資訊
                    var gpuList = GetGpuInfo();

                    return new SystemSnapshot
                    {
                        OsVersion = Environment.OSVersion.VersionString,
                        OsBuild = osBuild,
                        ComputerName = Environment.MachineName,
                        DomainOrWorkgroup = domain,
                        IpAddresses = JsonSerializer.Serialize(ipList),
                        TotalRamMb = GetTotalRamMb(),
                        CpuName = GetCpuName(),
                        GpuInfo = JsonSerializer.Serialize(gpuList),
                        CapturedAt = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Critical error during system snapshot collection.");
                    return new SystemSnapshot { CapturedAt = DateTime.Now };
                }
            }, ct);
        }

        private string GetWorkgroup()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Workgroup FROM Win32_ComputerSystem");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    return obj["Workgroup"]?.ToString() ?? "WORKGROUP";
                }
            }
            catch { }
            return "WORKGROUP";
        }

        private long GetTotalRamMb()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    var bytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                    return (long)(bytes / 1048576);
                }
            }
            catch { }
            return 0;
        }

        private string GetCpuName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    return obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                }
            }
            catch { }
            return "Unknown CPU";
        }

        private List<object> GetGpuInfo()
        {
            var gpus = new List<object>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion, VideoProcessor FROM Win32_VideoController");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    // AdapterRAM 在某些環境可能為 null
                    var ramBytes = obj["AdapterRAM"] != null ? Convert.ToUInt64(obj["AdapterRAM"]) : 0;
                    gpus.Add(new
                    {
                        Name = obj["Name"]?.ToString(),
                        AdapterRamMb = (long)(ramBytes / 1048576),
                        DriverVersion = obj["DriverVersion"]?.ToString(),
                        VideoProcessor = obj["VideoProcessor"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to query GPU info via WMI.");
            }
            return gpus;
        }
    }
}
