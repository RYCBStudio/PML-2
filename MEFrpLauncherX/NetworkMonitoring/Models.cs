using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MEFrpLauncherX.NetworkMonitoring;

// 网络接口信息
public class NetworkInterfaceInfo
{
    public string Name
    {
        get;
        set;
    } = string.Empty;

    public string Description
    {
        get;
        set;
    } = string.Empty;

    public string Id
    {
        get;
        set;
    } = string.Empty;

    public bool IsOperational
    {
        get;
        set;
    }

    public long Speed
    {
        get;
        set;
    } // 接口速度 (bps)
}

public class RawInfo
{
    public string Name
    {
        get;
        set;
    } = "";

    public string InterfaceDescription
    {
        get;
        set;
    } = "";

    public int InterfaceIndex
    {
        get;
        set;
    } = 0;

    public string Status
    {
        get;
        set;
    } = "";

    public string LinkSpeed
    {
        get;
        set;
    } = "";
}

// 网络流量数据
// 网络流量数据（累计值）
public class NetworkTraffic
{
    public string InterfaceId
    {
        get;
        set;
    } = string.Empty;

    public long TotalBytesReceived
    {
        get;
        set;
    } // 累计接收字节

    public long TotalBytesSent
    {
        get;
        set;
    } // 累计发送字节

    public long TotalPacketsReceived
    {
        get;
        set;
    } // 累计接收包数

    public long TotalPacketsSent
    {
        get;
        set;
    } // 累计发送包数

    public DateTime Timestamp
    {
        get;
        set;
    } = DateTime.UtcNow;
}

// 网络监控接口
public interface INetworkMonitor
{
    Task<IEnumerable<NetworkInterfaceInfo>> GetNetworkInterfacesAsync();
    Task<NetworkTraffic> GetTrafficDataAsync(string interfaceId); // 获取累计流量
    void StartMonitoring(string interfaceId, TimeSpan updateInterval, NetworkTraffic? initialTraffic);
    void StopMonitoring(string interfaceId);
    event EventHandler<NetworkTraffic>? TrafficUpdated; // 改为流量更新事件
}

// 网络速度数据
public class NetworkSpeed
{
    public string InterfaceId
    {
        get;
        set;
    } = string.Empty;

    public double DownloadSpeed
    {
        get;
        set;
    } // B/s

    public double UploadSpeed
    {
        get;
        set;
    } // B/s

    public DateTime Timestamp
    {
        get;
        set;
    } = DateTime.UtcNow;
}