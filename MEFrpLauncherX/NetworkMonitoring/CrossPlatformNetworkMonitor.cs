using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MEFrpLauncherX.NetworkMonitoring
{
    public class CrossPlatformNetworkMonitor : INetworkMonitor, IDisposable
    {
        private readonly Dictionary<string, Timer> _monitoringTimers = new();
        private readonly object _lockObject = new();
        private bool _disposed = false;

        public event EventHandler<NetworkTraffic>? TrafficUpdated;

        public async Task<IEnumerable<NetworkInterfaceInfo>> GetNetworkInterfacesAsync()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await GetWindowsNetworkInterfacesAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return await GetLinuxNetworkInterfacesAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return await GetMacOSNetworkInterfacesAsync();
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform");
            }
        }

        public async Task<NetworkTraffic> GetTrafficDataAsync(string interfaceId)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await GetWindowsTrafficDataAsync(interfaceId);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return await GetLinuxTrafficDataAsync(interfaceId);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return await GetMacOSTrafficDataAsync(interfaceId);
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform");
            }
        }

        public void StartMonitoring(string interfaceId, TimeSpan updateInterval, NetworkTraffic? initialTraffic = null)
        {
            initialTraffic ??= new()
            {
                InterfaceId = "",
                TotalBytesReceived = 0,
                TotalBytesSent = 0,
                TotalPacketsReceived = 0,
                TotalPacketsSent = 0,
                Timestamp = default
            };
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
                // 使用WMIC命令获取网络适配器信息
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments =
                        "/c wmic nic where \"NetEnabled=true\" get Name, Description, DeviceID, Speed /format:csv",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var process = Process.Start(processStartInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines.Skip(2)) // 跳过标题行
                    {
                        var parts = line.Split(',');
                        if (parts.Length >= 5)
                        {
                            interfaces.Add(new NetworkInterfaceInfo
                            {
                                Id = parts[1].Trim(), // DeviceID
                                Name = parts[3].Trim(), // Name
                                Description = parts[2].Trim(), // Description
                                IsOperational = true,
                                Speed = long.TryParse(parts[4].Trim(), out var speed) ? speed : 0
                            });
                        }
                    }
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
            try
            {
                // 使用WMIC获取网络流量统计
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments =
                        $"/c wmic path Win32_PerfRawData_Tcpip_NetworkInterface where \"Name='{interfaceId.Replace('(', '[').Replace(')', ']')}'\" get BytesReceivedPersec,BytesSentPersec,BytesTotalPersec,PacketsReceivedPersec,PacketsSentPersec /format:csv",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var process = Process.Start(processStartInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 2)
                    {
                        var data = lines[2].Split(',');
                        if (data.Length >= 6)
                        {
                            long.TryParse(data[2].Trim(), out var bytesReceived);
                            long.TryParse(data[3].Trim(), out var bytesSent);
                            long.TryParse(data[4].Trim(), out var packetsReceived);
                            long.TryParse(data[5].Trim(), out var packetsSent);

                            return new NetworkTraffic
                            {
                                InterfaceId = interfaceId,
                                TotalBytesReceived = bytesReceived,
                                TotalBytesSent = bytesSent,
                                TotalPacketsReceived = packetsReceived,
                                TotalPacketsSent = packetsSent
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Windows traffic data: {ex.Message}");
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
    }
}