using MEFrpLauncherX.NetworkMonitoring;

namespace PML2Test;

class Program
{
    private readonly INetworkMonitor _networkMonitor;

    public Program()
    {
        _networkMonitor = new CrossPlatformNetworkMonitor();
        _networkMonitor.TrafficUpdated += OnTrafficUpdated; // 改为TrafficUpdated事件
    }

    static async Task Main(string[] args)
    {
        var program = new Program();
        await program.InitializeAsync();
        
        Console.WriteLine("网络流量监控已启动，按任意键退出...");
        Console.ReadKey();
        
        program.Cleanup();
    }

    public async Task InitializeAsync()
    {
        try
        {
            Console.WriteLine("正在获取网络接口...");
            
            // 获取网络接口
            var interfaces = await _networkMonitor.GetNetworkInterfacesAsync();
            
            Console.WriteLine($"找到 {interfaces.Count()} 个网络接口:");
            foreach (var iface in interfaces)
            {
                Console.WriteLine($"  - {iface.Name} ({iface.Description}) - 状态: {(iface.IsOperational ? "运行中" : "未运行")}");
            }

            var primaryInterface = interfaces.FirstOrDefault(i => i.IsOperational);
            
            if (primaryInterface != null)
            {
                Console.WriteLine($"开始监控接口: {primaryInterface.Name}");
                
                // 先获取一次初始流量数据
                var initialTraffic = await _networkMonitor.GetTrafficDataAsync(primaryInterface.Id);
                Console.WriteLine("初始流量数据:");
                Console.WriteLine($"  累计接收: {FormatBytes(initialTraffic.TotalBytesReceived)}");
                Console.WriteLine($"  累计发送: {FormatBytes(initialTraffic.TotalBytesSent)}");
                Console.WriteLine($"  累计接收包: {initialTraffic.TotalPacketsReceived}");
                Console.WriteLine($"  累计发送包: {initialTraffic.TotalPacketsSent}");
                
                // 开始监控，每2秒更新一次
                _networkMonitor.StartMonitoring(primaryInterface.Id, TimeSpan.FromSeconds(2), initialTraffic);
            }
            else
            {
                Console.WriteLine("没有找到运行中的网络接口！");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"初始化失败: {ex.Message}");
            Console.WriteLine($"详细错误: {ex}");
        }
    }

    private void OnTrafficUpdated(object? sender, NetworkTraffic traffic)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 流量统计:");
        Console.WriteLine($"  累计接收: {FormatBytes(traffic.TotalBytesReceived)}");
        Console.WriteLine($"  累计发送: {FormatBytes(traffic.TotalBytesSent)}");
        Console.WriteLine($"  累计接收包: {traffic.TotalPacketsReceived}");
        Console.WriteLine($"  累计发送包: {traffic.TotalPacketsSent}");
        Console.WriteLine("---");
    }

    private string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var counter = 0;
        decimal number = bytes;
        
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:n2} {suffixes[counter]}";
    }

    public void Cleanup()
    {
        Console.WriteLine("正在清理资源...");
        _networkMonitor.TrafficUpdated -= OnTrafficUpdated;
        (_networkMonitor as IDisposable)?.Dispose();
    }
}