using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;

namespace MEFrpLauncherX.NetworkMonitoring;

public class CrossPlatformNetworkMonitor : INetworkMonitor, IDisposable
{
    private readonly object _lockObject = new();
    private readonly Dictionary<string, Timer> _monitoringTimers = new();
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lockObject)
            {
                foreach (var timer in _monitoringTimers.Values)
                {
                    timer?.Dispose();
                }

                _monitoringTimers.Clear();
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    public event EventHandler<NetworkTraffic>? TrafficUpdated;

    public async Task<IEnumerable<NetworkInterfaceInfo>> GetNetworkInterfacesAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await GetWindowsNetworkInterfacesAsync();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await GetLinuxNetworkInterfacesAsync();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await GetMacOSNetworkInterfacesAsync();
        }

        throw new PlatformNotSupportedException("Unsupported platform");
    }

    public async Task<NetworkTraffic> GetTrafficDataAsync(string interfaceId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await GetWindowsTrafficDataAsync(interfaceId);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await GetLinuxTrafficDataAsync(interfaceId);
        }

        if (OperatingSystem.IsMacOS())
        {
            return await GetMacOSTrafficDataAsync(interfaceId);
        }

        throw new PlatformNotSupportedException("Unsupported platform");
    }

    public void StartMonitoring(string interfaceId, TimeSpan updateInterval, NetworkTraffic? initialTraffic = null)
    {
        lock (_lockObject)
        {
            if (_monitoringTimers.ContainsKey(interfaceId))
            {
                StopMonitoring(interfaceId);
            }

            var timer = new Timer(async _ =>
            {
                try
                {
                    var traffic = await GetTrafficDataAsync(interfaceId);
                    TrafficUpdated?.Invoke(this, traffic);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error monitoring network traffic: {ex.Message}");
                }
            }, null, TimeSpan.Zero, updateInterval);

            _monitoringTimers[interfaceId] = timer;
        }
    }

    public void StopMonitoring(string interfaceId)
    {
        lock (_lockObject)
        {
            if (_monitoringTimers.TryGetValue(interfaceId, out var timer))
            {
                timer?.Dispose();
                _monitoringTimers.Remove(interfaceId);
            }
        }
    }

    #region Windows Implementation

    private async Task<IEnumerable<NetworkInterfaceInfo>> GetWindowsNetworkInterfacesAsync()
    {
        var interfaces = new List<NetworkInterfaceInfo>();
        try
        {
            // 1. 使用 System.Net.NetworkInformation 获取所有启用的网卡
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                            !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                            !n.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                            !n.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nics.Count == 0)
                return interfaces;

            // 2. 查询 WMI 获取所有性能计数器实例名称
            using var session = CimSession.Create(null);
            var perfInstances = await Task.Run(() => session.QueryInstances(
                @"root\cimv2", "WQL", "SELECT Name FROM Win32_PerfRawData_Tcpip_NetworkInterface"));
            var perfNames = perfInstances
                .Select(obj => obj.CimInstanceProperties["Name"]?.Value?.ToString())
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            Debug.WriteLine("可用性能计数器名称: " + string.Join(", ", perfNames));

            // 3. 查询 Win32_NetworkAdapter 获取 DeviceID（用于索引匹配）
            var adapterQuery = "SELECT DeviceID, Description FROM Win32_NetworkAdapter WHERE NetEnabled = true";
            var adapters = await Task.Run(() => session.QueryInstances(@"root\cimv2", "WQL", adapterQuery));
            var deviceIdMap = adapters.ToDictionary(
                a => a.CimInstanceProperties["Description"]?.Value?.ToString() ?? "",
                a => a.CimInstanceProperties["DeviceID"]?.Value?.ToString() ?? "");

            // 4. 为每个网卡匹配性能计数器名称
            foreach (var nic in nics)
            {
                var matchedName = "";
                var desc = nic.Description ?? "";
                var name = nic.Name ?? "";

                // 4a. 将描述中的圆括号替换为方括号，以匹配性能计数器名称格式
                var normalizedDesc = desc.Replace('(', '[').Replace(')', ']');

                // 通过标准化描述进行包含匹配
                matchedName = perfNames.FirstOrDefault(p =>
                    p.Contains(normalizedDesc, StringComparison.OrdinalIgnoreCase) ||
                    normalizedDesc.Contains(p, StringComparison.OrdinalIgnoreCase)) ?? "";

                // 4b. 若失败，通过 IPv4 接口索引匹配
                if (string.IsNullOrEmpty(matchedName))
                {
                    var ipProps = nic.GetIPProperties().GetIPv4Properties();
                    if (ipProps != null)
                    {
                        var index = ipProps.Index;
                        matchedName = perfNames.FirstOrDefault(p =>
                            p.EndsWith($"#{index}") || p.EndsWith($"_{index}")) ?? "";
                    }
                }

                // 4c. 尝试 IPv6 索引匹配，但忽略异常（某些系统未启用 IPv6）
                if (string.IsNullOrEmpty(matchedName))
                {
                    try
                    {
                        var ipv6Props = nic.GetIPProperties().GetIPv6Properties();
                        if (ipv6Props != null)
                        {
                            var index = ipv6Props.Index;
                            matchedName = perfNames.FirstOrDefault(p =>
                                p.EndsWith($"#{index}") || p.EndsWith($"_{index}")) ?? "";
                        }
                    }
                    catch
                    {
                        // IPv6 未配置，忽略
                    }
                }

                // 4d. 若仍失败，通过 DeviceID 匹配（从 Win32_NetworkAdapter 获取）
                if (string.IsNullOrEmpty(matchedName) && deviceIdMap.TryGetValue(desc, out var deviceId))
                {
                    matchedName = perfNames.FirstOrDefault(p =>
                        p.EndsWith($"#{deviceId}") || p.EndsWith($"_{deviceId}")) ?? "";
                }

                // 4e. 最后降级：通过网卡名称（如 "WLAN"）模糊匹配
                if (string.IsNullOrEmpty(matchedName))
                {
                    matchedName = perfNames.FirstOrDefault(p =>
                        p.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                        name.Contains(p, StringComparison.OrdinalIgnoreCase)) ?? "";
                }

                // 调试输出
                if (string.IsNullOrEmpty(matchedName))
                    Debug.WriteLine($"未能为网卡 '{name}' (描述: {desc}) 匹配到性能计数器名称");
                else
                    Debug.WriteLine($"网卡 '{name}' 匹配到: {matchedName}");

                interfaces.Add(new NetworkInterfaceInfo
                {
                    Id = matchedName, // 可能为空字符串
                    Name = name,
                    Description = desc,
                    IsOperational = true,
                    Speed = nic.Speed
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting Windows network interfaces: {ex.Message}");
        }

        return interfaces;
    }

    private async Task<NetworkTraffic> GetWindowsTrafficDataAsync(string interfaceId)
    {
        if (string.IsNullOrEmpty(interfaceId))
            return new NetworkTraffic { InterfaceId = interfaceId };

        try
        {
            using var session = CimSession.Create(Environment.MachineName);
            var safeName = interfaceId.Replace("'", "''");
            var query = $@"
            SELECT BytesReceivedPersec, BytesSentPersec, 
                   PacketsReceivedPersec, PacketsSentPersec 
            FROM Win32_PerfRawData_Tcpip_NetworkInterface 
            WHERE Name = '{safeName}'";

            var results = await Task.Run(() => session.QueryInstances(@"root\cimv2", "WQL", query));
            var cimObj = results.FirstOrDefault();

            if (cimObj != null)
            {
                long.TryParse(cimObj.CimInstanceProperties["BytesReceivedPersec"]?.Value?.ToString(),
                    out var bytesRecv);
                long.TryParse(cimObj.CimInstanceProperties["BytesSentPersec"]?.Value?.ToString(), out var bytesSent);
                long.TryParse(cimObj.CimInstanceProperties["PacketsReceivedPersec"]?.Value?.ToString(),
                    out var packetsRecv);
                long.TryParse(cimObj.CimInstanceProperties["PacketsSentPersec"]?.Value?.ToString(),
                    out var packetsSent);
                Debug.WriteLine($"[WMI] Interface: {interfaceId}, BytesRecv: {bytesRecv}, BytesSent: {bytesSent}");
                return new NetworkTraffic
                {
                    InterfaceId = interfaceId,
                    TotalBytesReceived = bytesRecv,
                    TotalBytesSent = bytesSent,
                    TotalPacketsReceived = packetsRecv,
                    TotalPacketsSent = packetsSent
                };
            }
            else
            {
                Debug.WriteLine($"未找到名为 '{interfaceId}' 的网络接口实例。");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error getting Windows traffic data: {ex.Message}");
        }

        return new NetworkTraffic { InterfaceId = interfaceId };
    }

    #endregion


    #region Linux Implementation

    private async Task<IEnumerable<NetworkInterfaceInfo>> GetLinuxNetworkInterfacesAsync()
    {
        var interfaces = new List<NetworkInterfaceInfo>();

        try
        {
            // 读取/proc/net/dev获取接口列表
            if (File.Exists("/proc/net/dev"))
            {
                var lines = await File.ReadAllLinesAsync("/proc/net/dev");

                // 跳过前两行（表头）
                foreach (var line in lines.Skip(2))
                {
                    var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0].Trim();
                        if (!string.IsNullOrWhiteSpace(name) && !name.Contains("lo"))
                        {
                            interfaces.Add(new NetworkInterfaceInfo
                            {
                                Id = name,
                                Name = name,
                                Description = $"Network Interface {name}",
                                IsOperational = await IsLinuxInterfaceUpAsync(name),
                                Speed = await GetLinuxInterfaceSpeedAsync(name)
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting Linux network interfaces: {ex.Message}");
        }

        return interfaces;
    }

    private async Task<NetworkTraffic> GetLinuxTrafficDataAsync(string interfaceId)
    {
        try
        {
            // 方法1: 读取/sys/class/net统计信息
            var basePath = $"/sys/class/net/{interfaceId}/statistics/";

            if (Directory.Exists($"/sys/class/net/{interfaceId}"))
            {
                var rxBytes = await ReadLinuxNetworkStat($"{basePath}rx_bytes");
                var txBytes = await ReadLinuxNetworkStat($"{basePath}tx_bytes");
                var rxPackets = await ReadLinuxNetworkStat($"{basePath}rx_packets");
                var txPackets = await ReadLinuxNetworkStat($"{basePath}tx_packets");

                return new NetworkTraffic
                {
                    InterfaceId = interfaceId,
                    TotalBytesReceived = rxBytes,
                    TotalBytesSent = txBytes,
                    TotalPacketsReceived = rxPackets,
                    TotalPacketsSent = txPackets
                };
            }

            // 方法2: 读取/proc/net/dev作为备选
            return await GetLinuxTrafficFromProcNetAsync(interfaceId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting Linux traffic data for {interfaceId}: {ex.Message}");
        }

        return new NetworkTraffic { InterfaceId = interfaceId };
    }

    private async Task<NetworkTraffic> GetLinuxTrafficFromProcNetAsync(string interfaceId)
    {
        try
        {
            if (File.Exists("/proc/net/dev"))
            {
                var lines = await File.ReadAllLinesAsync("/proc/net/dev");

                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith($"{interfaceId}:"))
                    {
                        var dataPart = line.Split(':')[1].Trim();
                        var parts = dataPart.Split([' '], StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length >= 16)
                        {
                            // 格式: bytes packets errs drop fifo frame compressed multicast|bytes packets errs drop fifo colls carrier compressed
                            long.TryParse(parts[0], out var rxBytes);
                            long.TryParse(parts[1], out var rxPackets);
                            long.TryParse(parts[8], out var txBytes);
                            long.TryParse(parts[9], out var txPackets);

                            return new NetworkTraffic
                            {
                                InterfaceId = interfaceId,
                                TotalBytesReceived = rxBytes,
                                TotalBytesSent = txBytes,
                                TotalPacketsReceived = rxPackets,
                                TotalPacketsSent = txPackets
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading /proc/net/dev for {interfaceId}: {ex.Message}");
        }

        return new NetworkTraffic { InterfaceId = interfaceId };
    }

    private async Task<long> ReadLinuxNetworkStat(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var content = await File.ReadAllTextAsync(filePath);
                if (long.TryParse(content.Trim(), out var result))
                {
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading Linux network stat {filePath}: {ex.Message}");
        }

        return 0;
    }

    private async Task<bool> IsLinuxInterfaceUpAsync(string interfaceName)
    {
        try
        {
            var operstateFile = $"/sys/class/net/{interfaceName}/operstate";
            if (File.Exists(operstateFile))
            {
                var state = await File.ReadAllTextAsync(operstateFile);
                return state.Trim().ToLower() == "up";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking interface state for {interfaceName}: {ex.Message}");
        }

        return false;
    }

    private async Task<long> GetLinuxInterfaceSpeedAsync(string interfaceName)
    {
        try
        {
            var speedFile = $"/sys/class/net/{interfaceName}/speed";
            if (File.Exists(speedFile))
            {
                var content = await File.ReadAllTextAsync(speedFile);
                if (long.TryParse(content.Trim(), out var speed) && speed > 0)
                {
                    return speed * 1000000; // 转换为bps
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting interface speed for {interfaceName}: {ex.Message}");
        }

        return 1000000000; // 默认1Gbps
    }

    #endregion

    #region macOS Implementation

    private async Task<IEnumerable<NetworkInterfaceInfo>> GetMacOSNetworkInterfacesAsync()
    {
        var interfaces = new List<NetworkInterfaceInfo>();

        try
        {
            // 使用networksetup命令
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"networksetup -listallnetworkservices | tail -n +2\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var interfaceNames = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var name in interfaceNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        interfaces.Add(new NetworkInterfaceInfo
                        {
                            Id = name.Trim(),
                            Name = name.Trim(),
                            Description = $"Network Service {name}",
                            IsOperational = await IsMacOSInterfaceUpAsync(name.Trim()),
                            Speed = 1000000000
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting macOS network interfaces: {ex.Message}");
        }

        return interfaces;
    }

    private async Task<NetworkTraffic> GetMacOSTrafficDataAsync(string interfaceId)
    {
        try
        {
            // 使用netstat命令获取累计流量
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"-c \"netstat -I {interfaceId} -b | tail -n +2 | head -1\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return ParseMacOSTrafficOutput(output, interfaceId);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting macOS traffic data for {interfaceId}: {ex.Message}");
        }

        return new NetworkTraffic { InterfaceId = interfaceId };
    }

    private NetworkTraffic ParseMacOSTrafficOutput(string output, string interfaceId)
    {
        try
        {
            var parts = output.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 10)
            {
                // netstat -I 输出格式:
                // Name  Mtu   Network       Address            Ipkts Ierrs     Ibytes    Opkts Oerrs     Obytes  Coll
                long.TryParse(parts[4], out var packetsIn); // Ipkts
                long.TryParse(parts[7], out var bytesIn); // Ibytes
                long.TryParse(parts[8], out var packetsOut); // Opkts
                long.TryParse(parts[11], out var bytesOut); // Obytes

                return new NetworkTraffic
                {
                    InterfaceId = interfaceId,
                    TotalBytesReceived = bytesIn,
                    TotalBytesSent = bytesOut,
                    TotalPacketsReceived = packetsIn,
                    TotalPacketsSent = packetsOut
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error parsing macOS traffic output: {ex.Message}");
        }

        return new NetworkTraffic { InterfaceId = interfaceId };
    }

    private async Task<bool> IsMacOSInterfaceUpAsync(string interfaceName)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments =
                    $"-c \"ifconfig {interfaceName} 2>/dev/null | grep -q 'status: active' && echo 'UP' || echo 'DOWN'\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                return output.Trim() == "UP";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking macOS interface state for {interfaceName}: {ex.Message}");
        }

        return false;
    }

    #endregion
}