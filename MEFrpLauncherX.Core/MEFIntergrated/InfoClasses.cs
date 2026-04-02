using System.Text.Json.Serialization;

namespace MEFrpLauncherX.Core.MEFIntergrated;
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
public class InfoClasses
{
    /// <summary>
    /// 统计信息
    /// </summary>
    public class ApiInfo<T>
    {
        /// <summary>
        /// 数据
        /// </summary>
        public T? data
        {
            get;
            set;
        }

        /// <summary>
        /// 消息
        /// </summary>
        public string message
        {
            get;
            set;
        }

        /// <summary>
        /// 状态码
        /// </summary>
        public int code
        {
            get;
            set;
        }
    }

    public class SignInfo
    {
        public int extraTraffic
        {
            get;
            set;
        }
    }

    public class ConfigInfo
    {
        public string config
        {
            get;
            set;
        }

        public string type
        {
            get;
            set;
        }
    }

    public class TrafficStatus
    {
        public string[] dates
        {
            get;
            set;
        }

        public long[] trafficIn
        {
            get;
            set;
        }

        public long[] trafficOut
        {
            get;
            set;
        }

        public long[] totalTraffic
        {
            get;
            set;
        }
    }


    public class FrpTokenInfo
    {
        public string token
        {
            get;
            set;
        }
    }

    public class ProxyInfo
    {
        public Nodes[] nodes
        {
            get;
            set;
        }

        public Proxies[] proxies
        {
            get;
            set;
        }
    }

    public class Nodes
    {
        public int nodeId
        {
            get;
            set;
        }

        public string name
        {
            get;
            set;
        }

        public string hostname
        {
            get;
            set;
        }
    }

    public class Proxies
    {
        public int proxyId
        {
            get;
            set;
        }

        public string username
        {
            get;
            set;
        }

        public string proxyName
        {
            get;
            set;
        }

        public string proxyType
        {
            get;
            set;
        }

        public bool isBanned
        {
            get;
            set;
        }

        public bool isDisabled
        {
            get;
            set;
        }

        public string localIp
        {
            get;
            set;
        }

        public int localPort
        {
            get;
            set;
        }

        public int remotePort
        {
            get;
            set;
        }

        public int nodeId
        {
            get;
            set;
        }

        public string runId
        {
            get;
            set;
        }

        public bool isOnline
        {
            get;
            set;
        }

        public string domain
        {
            get;
            set;
        }

        public int lastStartTime
        {
            get;
            set;
        }

        public int lastCloseTime
        {
            get;
            set;
        }

        public string clientVersion
        {
            get;
            set;
        }

        public string proxyProtocolVersion
        {
            get;
            set;
        }

        public bool useEncryption
        {
            get;
            set;
        }

        public bool useCompression
        {
            get;
            set;
        }

        public string locations
        {
            get;
            set;
        }

        public string accessKey
        {
            get;
            set;
        }

        public string hostHeaderRewrite
        {
            get;
            set;
        }

        public string httpPlugin
        {
            get;
            set;
        }

        public string crtPath
        {
            get;
            set;
        }

        public string keyPath
        {
            get;
            set;
        }

        public string requestHeaders
        {
            get;
            set;
        }

        public string responseHeaders
        {
            get;
            set;
        }

        public string httpUser
        {
            get;
            set;
        }

        public string httpPassword
        {
            get;
            set;
        }

        public string transportProtocol
        {
            get;
            set;
        }
    }
    
    public class KickProxyInfo
    {
        public int proxyId
        {
            get;
            set;
        }
    }
    
    public class ToggleProxyInfo
    {
        public int proxyId
        {
            get;
            set;
        }

        public bool isDisabled
        {
            get;
            set;
        }
    }

    public class FreePortBody
    {
        public int nodeId
        {
            get;
            set;
        }

        public string protocol
        {
            get;
            set;
        }
    }


    public class NodesListInfo
    {
        public NodesList[] NodesList
        {
            get;
            set;
        }
    }

    public class NodesList
    {
        /// <summary>
        /// 节点ID
        /// </summary>
        public int nodeId
        {
            get;
            set;
        }

        /// <summary>
        /// 节点名称
        /// </summary>
        public string name
        {
            get;
            set;
        }

        /// <summary>
        /// 节点主机名
        /// </summary>
        public string hostname
        {
            get;
            set;
        }

        /// <summary>
        /// 描述
        /// </summary>
        public string description
        {
            get;
            set;
        }

        public string token
        {
            get;
            set;
        }

        public int servicePort
        {
            get;
            set;
        }

        public int adminPort
        {
            get;
            set;
        }


        public string adminPass
        {
            get;
            set;
        }

        /// <summary>
        /// 允许的分组
        /// </summary>
        public string allowGroup
        {
            get;
            set;
        }

        /// <summary>
        /// 允许的端口
        /// </summary>
        public string allowPort
        {
            get;
            set;
        }

        /// <summary>
        /// 允许的协议
        /// </summary>
        public string allowType
        {
            get;
            set;
        }

        /// <summary>
        /// 地区
        /// </summary>
        public string region
        {
            get;
            set;
        }

        /// <summary>
        /// 带宽
        /// </summary>
        public string bandwidth
        {
            get;
            set;
        }

        /// <summary>
        /// 是否在线
        /// </summary>
        public bool isOnline
        {
            get;
            set;
        }

        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool isDisabled
        {
            get;
            set;
        }

        /// <summary>
        /// 总入站流量
        /// </summary>
        public int totalTrafficIn
        {
            get;
            set;
        }

        /// <summary>
        /// 总出站流量
        /// </summary>
        public int totalTrafficOut
        {
            get;
            set;
        }

        /// <summary>
        /// 在线/离线时间
        /// </summary>
        public int upTime
        {
            get;
            set;
        }

        /// <summary>
        /// 运行的MEFRPS版本
        /// </summary>
        public string version
        {
            get;
            set;
        }
    }

    public class NodeNameList
    {
        public int nodeId
        {
            get;
            set;
        }

        public string name
        {
            get;
            set;
        }

        public string hostname
        {
            get;
            set;
        }
    }


    public class NodesStatusInfo
    {
        public NodeStatus[] NodesStatus
        {
            get;
            set;
        }
    }

    public class NodeStatus
    {
        /// <summary>
        /// 节点ID
        /// </summary>
        public int nodeId
        {
            get;
            set;
        }

        /// <summary>
        /// 节点名称
        /// </summary>
        public string name
        {
            get;
            set;
        }

        /// <summary>
        /// 入站流量
        /// </summary>
        public long totalTrafficIn
        {
            get;
            set;
        }

        /// <summary>
        /// 出站流量
        /// </summary>
        public long totalTrafficOut
        {
            get;
            set;
        }

        /// <summary>
        /// 连接数
        /// </summary>
        public int onlineClient
        {
            get;
            set;
        }

        /// <summary>
        /// 在线隧道数
        /// </summary>
        public int onlineProxy
        {
            get;
            set;
        }

        /// <summary>
        /// 是否在线
        /// </summary>
        public bool isOnline
        {
            get;
            set;
        }

        /// <summary>
        /// 运行的MEFRPS版本
        /// </summary>
        public string version
        {
            get;
            set;
        }

        /// <summary>
        /// 运行时间/离线时间
        /// </summary>
        public int uptime
        {
            get;
            set;
        }

        /// <summary>
        /// 当前连接数？
        /// </summary>
        public int curConns
        {
            get;
            set;
        }

        /// <summary>
        /// 负载百分数
        /// </summary>
        public int loadPercent
        {
            get;
            set;
        }
    }


    public class PublicData
    {
        /// <summary>
        /// 用户数
        /// </summary>
        public int users
        {
            get;
            set;
        }

        /// <summary>
        /// 节点数
        /// </summary>
        public int nodes
        {
            get;
            set;
        }

        /// <summary>
        /// 隧道数
        /// </summary>
        public int proxies
        {
            get;
            set;
        }

        /// <summary>
        /// 流量
        /// </summary>
        public long traffic
        {
            get;
            set;
        }
    }

    public class SystemStatus
    {
        /// <summary>
        /// 状态简码
        /// 0 正常 1 降级 2 离线
        /// </summary>
        public int status
        {
            get;
            set;
        }

        /// <summary>
        /// 状态说明
        /// </summary>
        public string remark
        {
            get;
            set;
        }
    }

    public class UserInfo4Login
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string username
        {
            get;
            set;
        }

        /// <summary>
        /// 密码
        /// </summary>
        public string password
        {
            get;
            set;
        }
    }

    public class UserInfo
    {
        /// <summary>
        /// 用户组
        /// </summary>
        public string group
        {
            get;
            set;
        }


        public string token
        {
            get;
            set;
        }

        /// <summary>
        /// 用户名
        /// </summary>
        public string username
        {
            get;
            set;
        }

        [JsonIgnore]
        public string Email
        {
            get;
            set;
        }
    }

    public class VaptchaInfo
    {
        public string token
        {
            get;
            set;
        }

        public string server
        {
            get;
            set;
        }
    }

    public class LoginInfo
    {
        public string username
        {
            get;
            set;
        }

        public string password
        {
            get;
            set;
        }

        public string captchaToken
        {
            get;
            set;
        }
    }

    public class ExtraUserInfo
    {
        /// <summary>
        /// 注册邮箱
        /// </summary>
        public string email
        {
            get;
            set;
        }

        /// <summary>
        /// 语义化用户组
        /// </summary>
        public string friendlyGroup
        {
            get;
            set;
        }

        /// <summary>
        /// 用户组ID
        /// </summary>
        public string group
        {
            get;
            set;
        }

        /// <summary>
        /// 入站带宽
        /// </summary>
        public int inBound
        {
            get;
            set;
        }

        /// <summary>
        /// 是否已实名
        /// </summary>
        public bool isRealname
        {
            get;
            set;
        }

        /// <summary>
        /// 最大隧道数
        /// </summary>
        public int maxProxies
        {
            get;
            set;
        }

        /// <summary>
        /// 出站带宽
        /// </summary>
        public int outBound
        {
            get;
            set;
        }

        /// <summary>
        /// 注册时间
        /// </summary>
        public int regTime
        {
            get;
            set;
        }

        /// <summary>
        /// 账户状态<br/>0-正常 1-封禁 2-流量超限
        /// </summary>
        public int status
        {
            get;
            set;
        }

        /// <summary>
        /// 是否已签到
        /// </summary>
        public bool todaySigned
        {
            get;
            set;
        }

        /// <summary>
        /// 总流量
        /// </summary>
        public int traffic
        {
            get;
            set;
        }

        /// <summary>
        /// 已使用隧道数
        /// </summary>
        public int usedProxies
        {
            get;
            set;
        }

        /// <summary>
        /// 用户ID
        /// </summary>
        public int userId
        {
            get;
            set;
        }

        /// <summary>
        /// 用户名
        /// </summary>
        public string username
        {
            get;
            set;
        }
    }
}