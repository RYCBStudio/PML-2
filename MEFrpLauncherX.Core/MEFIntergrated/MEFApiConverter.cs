using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Storage;
using RestSharp;
using RYCB.PML.MEFrpCaptchaLib;
using static MEFrpLauncherX.Core.MEFIntergrated.InfoClasses;

// ReSharper disable SuspiciousLockOverSynchronizationPrimitive
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8604 // 引用类型参数可能为 null。
#pragma warning disable CS8601 // 引用类型赋值可能为 null。
#pragma warning disable CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
#pragma warning disable CS8603 // 可能返回 null 引用。
#pragma warning disable CS8602 // 解引用可能出现空引用。
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。

namespace MEFrpLauncherX.Core.MEFIntergrated;

public class MEpiConverter
{
    public const string BaseApiUrl = "https://api.mefrp.com/api/";

    // Backing fields for node infos (cached)
    private static NodesListInfo? _nodesListInfo;
    private static NodesStatusInfo? _nodesStatusInfo;

    // SemaphoreSlim for async initialization to prevent concurrent initializations
    private static readonly SemaphoreSlim _nodesListSemaphore = new(1, 1);
    private static readonly SemaphoreSlim _nodesStatusSemaphore = new(1, 1);

    public static RestClient? CurrentClient
    {
        get;
        set;
    }

    /// <summary>
    ///     当前的公共信息
    /// </summary>
    public static ApiInfo<PublicData> CurrentPublicInfo
    {
        get;
        private set;
    } = new();

    /// <summary>
    ///     当前的用户信息
    /// </summary>
    public static ApiInfo<UserInfo> CurrentUserInfo
    {
        get;
        set;
    }

    /// <summary>
    ///     当前的节点List信息（仅返回缓存，不触发网络请求）
    /// </summary>
    public static NodesListInfo? CurrentNodesListInfo
    {
        get => Volatile.Read(ref _nodesListInfo);
        set
        {
            lock (_nodesListSemaphore)
            {
                _nodesListInfo = value;
            }
        }
    }

    /// <summary>
    ///     当前的节点状态信息（仅返回缓存，不触发网络请求）
    /// </summary>
    public static NodesStatusInfo? CurrentNodesStatusInfo
    {
        get => Volatile.Read(ref _nodesStatusInfo);
        set
        {
            lock (_nodesStatusSemaphore)
            {
                _nodesStatusInfo = value;
            }
        }
    }

    private static RestClient CreateClient(string endpoint)
    {
        return new RestClient(new RestClientOptions(BaseApiUrl + endpoint)
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UserAgent = OperatingSystem.IsAndroid() ? "RYCB-PML2/Android 0.0.1" : $"RYCB-PML2/Desktop {App.Version}",
            Timeout = TimeSpan.FromSeconds(3)
        });
    }

    private static RestRequest CreateRequest(Method method = Method.Get, bool withAuthorization = true)
    {
        var request = new RestRequest { Method = method };
        if (method != Method.Get)
        {
            request.AddHeader("Content-Type", "application/json");
        }

        if (UserCache.CurrentUser?.token != null && withAuthorization)
        {
            request.AddHeader("Authorization", $"Bearer {UserCache.CurrentUser.token}");
        }

        return request;
    }

    private static void HandleResponse<T>(ApiInfo<T> response)
    {
        if (response == null)
        {
            return;
        }

        if (response.code != 200)
        {
            Growl.Error(response.message);
        }
        else
        {
            if (!ConfigManager.CurrentConfig.DoNotShowSuccessMsg)
            {
                Growl.Success(response.message);
            }
        }
    }

    private static async Task<ApiInfo<T>> ExecuteRequestAsync<T>(RestRequest request, string endpoint,
        string operationName)
    {
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + endpoint}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger.Log($"正在获取{operationName}", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = $"正在获取{operationName}";

        using var client = CreateClient(endpoint);

        var response = await client.ExecuteAsync(request).ConfigureAwait(false);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        if (string.IsNullOrEmpty(response.Content))
        {
            var fallBack = new ApiInfo<T>
            {
                code = 0,
                message = "无法获取api信息",
                data = default
            };
            HandleResponse(fallBack);
            return fallBack;
        }

        if (response.Content.StartsWith("<"))
        {
            return new ApiInfo<T>
            {
                code = 502,
                message = "API回源失败, 无法获取api信息",
                data = default
            };
        }

        var result =
            JsonSerializer.Deserialize<ApiInfo<T>>(response.Content ?? "", AppJsonSerializerContext.Default.Options) ??
            new ApiInfo<T>
            {
                code = 0,
                message = "无法获取api信息",
                data = default
            };

        HandleResponse(result);
        MainWindowViewModel.Instance?.AppMessage = $"完成, 返回代码: {result.code}";
        return result;
    }

    public static async Task<ChallengeInfo> PostChallengeAsync(string challengeContent)
    {
        App.CurrentLogger.Log("Sending Captcha Challenge Request", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        request.AddParameter("application/json", challengeContent, ParameterType.RequestBody);
        using var client = new RestClient(Constants.ChallengeUrl);
        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return JsonSerializer.Deserialize<ChallengeInfo>(response.Content ?? "",
            AppJsonSerializerContext.Default.ChallengeInfo)!;
    }

    public static async Task<(CaptchaResultX, string)> GetRedeemAsync(string redeemBody)
    {
        App.CurrentLogger.Log("Sending Captcha Challenge Request", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        request.AddParameter("application/json", redeemBody, ParameterType.RequestBody);
        using var client = new RestClient(Constants.RedeemUrl);
        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return (
            JsonSerializer.Deserialize<CaptchaResultX>(response.Content ?? "",
                AppJsonSerializerContext.Default.CaptchaResultX)!, response.Content ?? "");
    }

    /// <summary>
    ///     获取系统状态
    /// </summary>
    /// <returns></returns>
    public static async Task<ApiInfo<SystemStatus?>> GetSystemStatusAsync()
    {
        ApiInfo<SystemStatus?> result = null;
        await AppAnalytics.TrackCostAsync("api.system.status", async () =>
        {
            result = await ExecuteRequestAsync<SystemStatus?>(CreateRequest(), "auth/system/status", "系统状态")
                .ConfigureAwait(false);
        });
        return result;
    }

    /// <summary>
    ///     获取重要公告
    /// </summary>
    /// <returns></returns>
    public static async Task<ApiInfo<string?>> GetPopupNoticeAsync()
    {
        ApiInfo<string?> result = null;
        result = await ExecuteRequestAsync<string?>(CreateRequest(), "auth/popupNotice", "重要公告");
        return result;
    }

    /// <summary>
    ///     异步获取公告
    /// </summary>
    /// <returns>公告内容</returns>
    public static async Task<ApiInfo<string>> GetNoticeAsync()
    {
        return await ExecuteRequestAsync<string>(CreateRequest(), "auth/notice", "公告");
    }

    /// <summary>
    ///     获取公共信息
    /// </summary>
    /// <returns>公共信息</returns>
    public static async Task<ApiInfo<PublicData>> GetPublicInfoAsync()
    {
        ApiInfo<PublicData> result = null;
        result = await ExecuteRequestAsync<PublicData>(CreateRequest(), "public/statistics", "公共信息");
        CurrentPublicInfo = result;
        return result;
    }

    /// <summary>
    ///     异步获取用户信息
    /// </summary>
    /// <returns>用户信息</returns>
    public static async Task<ApiInfo<ExtraUserInfo>> GetExtraUserInfoAsync()
    {
        ApiInfo<ExtraUserInfo> result = null;
        await AppAnalytics.TrackCostAsync("api.user-info", async () =>
        {
            result = await ExecuteRequestAsync<ExtraUserInfo>(CreateRequest(), "auth/user/info", "详细的用户信息");
        });
        return result;
    }

    public static string GetCaptchaResult(string code)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(code));
            // 支持两种格式：token|| 和 token||other_data
            return raw;
        }
        catch
        {
            return code; // 如果解码失败，直接返回原始代码
        }
    }

    /// <summary>
    ///     发送签到请求
    /// </summary>
    /// <param name="code">人机验证码</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static (bool, string?) SendSignRequest(string code)
    {
        App.CurrentLogger.Log("正在发送签到请求", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        var cr = GetCaptchaResult(code);
        var body = JsonSerializer.Serialize(new
        {
            captchaToken = cr
        }, new JsonSerializerOptions { WriteIndented = true });
        request.AddParameter("application/json", body, ParameterType.RequestBody);
        using var client = CreateClient("auth/user/sign");
        var response = client.Execute(request);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return (response.Content?.Contains("成功") ?? false, response.Content);
    }

    /// <summary>
    ///     发送签到请求 (异步)
    /// </summary>
    /// <param name="code">人机验证码</param>
    /// <returns>(是否成功，返回的 response 内容)</returns>
    public static async Task<(bool, string?)> SendSignRequestAsync(string code)
    {
        App.CurrentLogger.Log("正在发送签到请求", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        var cr = GetCaptchaResult(code);
        var body = JsonSerializer.Serialize(new
        {
            captchaToken = cr
        }, new JsonSerializerOptions { WriteIndented = true });
        request.AddParameter("application/json", body, ParameterType.RequestBody);
        using var client = CreateClient("auth/user/sign");

        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态：{response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        (bool, string?) result = (response.Content?.Contains("成功") ?? false, response.Content);
        return result;
    }

    /// <summary>
    ///     发送登录请求
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="captchaCode">人机验证码</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static (bool, string?) SendLoginInfo(string username, string password, string captchaCode)
    {
        App.CurrentLogger.Log("正在发送登录请求", port: EnumLogPort.Client, module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);

        var body = JsonSerializer.Serialize(new LoginInfo
        {
            username = username,
            password = password,
            captchaToken = GetCaptchaResult(captchaCode)
        }, AppJsonSerializerContext.Default.LoginInfo);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("public/login");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return (response.Content?.Contains("成功") ?? false, response.Content);
    }

    /// <summary>
    ///     获取节点状态
    /// </summary>
    /// <returns>一个"单个节点状态"数组。</returns>
    public static async Task<ApiInfo<NodeStatus[]>> GetNodesStatusAsync()
    {
        ApiInfo<NodeStatus[]> result = null;
        await AppAnalytics.TrackCostAsync("api.nodes.status", async () =>
        {
            result = await ExecuteRequestAsync<NodeStatus[]>(CreateRequest(), "auth/node/status", "节点状态");
        });

        if (result is not { data: not null })
        {
            return result;
        }

        // 存储到缓存，线程安全
        lock (_nodesStatusSemaphore)
        {
            _nodesStatusInfo ??= new NodesStatusInfo();
            _nodesStatusInfo.NodesStatus = result.data;
        }

        return result;
    }

    /// <summary>
    ///     获取节点信息
    /// </summary>
    /// <returns>一个"单个节点信息"数组。</returns>
    public static async Task<ApiInfo<NodesList[]>> GetNodesInfoAsync()
    {
        ApiInfo<NodesList[]> result = null;
        await AppAnalytics.TrackCostAsync("api.nodes.info", async () =>
        {
            result = await ExecuteRequestAsync<NodesList[]>(CreateRequest(), "auth/node/list", "节点信息");
        });
        return result;
    }

    /// <summary>
    ///     获取已创建隧道的节点连接地址
    /// </summary>
    /// <returns></returns>
    public static async Task<ApiInfo<NodeNameList[]>> GetNodesNameListAsync()
    {
        ApiInfo<NodeNameList[]> result = null;
        await AppAnalytics.TrackCostAsync("api.nodes.name-list", async () =>
        {
            result = await ExecuteRequestAsync<NodeNameList[]>(CreateRequest(), "auth/node/nameList", "已连接节点信息");
        });
        return result;
    }

    /// <summary>
    ///     确保节点列表缓存已初始化（线程安全，幂等）
    /// </summary>
    public static async Task<NodesListInfo?> EnsureNodesListInfoAsync(CancellationToken cancellationToken = default)
    {
        // Fast path
        if (Volatile.Read(ref _nodesListInfo) != null)
        {
            return _nodesListInfo;
        }

        await _nodesListSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_nodesListInfo != null)
            {
                return _nodesListInfo;
            }

            var apiResult = await GetNodesInfoAsync();
            if (apiResult is not { data: not null })
            {
                return _nodesListInfo;
            }

            var local = new NodesListInfo { NodesList = apiResult.data };
            Volatile.Write(ref _nodesListInfo, local);
            return local;
        }
        finally
        {
            _nodesListSemaphore.Release();
        }
    }

    /// <summary>
    ///     确保节点状态缓存已初始化（线程安全，幂等）
    /// </summary>
    public static async Task<NodesStatusInfo?> EnsureNodesStatusInfoAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _nodesStatusInfo) != null)
        {
            return _nodesStatusInfo;
        }

        await _nodesStatusSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_nodesStatusInfo != null)
            {
                return _nodesStatusInfo;
            }

            var apiResult = await GetNodesStatusAsync().ConfigureAwait(false);
            if (apiResult is { data: not null })
            {
                var local = new NodesStatusInfo { NodesStatus = apiResult.data };
                Volatile.Write(ref _nodesStatusInfo, local);
                return local;
            }

            return _nodesStatusInfo;
        }
        finally
        {
            _nodesStatusSemaphore.Release();
        }
    }

    /// <summary>
    ///     获取空闲端口
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <param name="protocol">要获取端口的协议, 只有tcp和udp。</param>
    /// <returns>空闲的端口，返回-1则说明获取失败</returns>
    public static async Task<ApiInfo<int>> GetFreePortAsync(int nodeId, string protocol = "tcp")
    {
        App.CurrentLogger.Log("正在获取空闲端口", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        protocol = protocol.ToLower();

        var body = JsonSerializer.Serialize(new FreePortBody
        {
            nodeId = nodeId,
            protocol = protocol
        }, AppJsonSerializerContext.Default.FreePortBody);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/node/freePort");
        var response = await client.ExecuteAsync(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonSerializer.Deserialize<ApiInfo<int>>(response.Content ?? """
            {
            "code": 0,
            "message": "无法获取api信息",
            "data": -1
            }
            """, AppJsonSerializerContext.Default.ApiInfoInt32) ?? new ApiInfo<int> { data = -1 };
        HandleResponse(result);
        return result;
    }

    /// <summary>
    ///     发送新建隧道请求
    /// </summary>
    /// <param name="body">要传入的请求体，详见<a href="https://apidoc.mefrp.com" /></param>
    /// <returns></returns>
    public static async Task<ApiInfo<object>> PostNewTunnelAsync(string body)
    {
        App.CurrentLogger.Log("正在发送新建隧道申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/create");

        var response = await client.ExecuteAsync(request);

        App.CurrentLogger.Log($"状态：{response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonSerializer.Deserialize<ApiInfo<object>>(response.Content ?? """
                         {
                         "code": 0,
                         "message": "无法获取 api 信息",
                         "data": {
                             "users": 0,
                             "nodes": 0,
                             "proxies": 0,
                             "traffic": 0
                             }
                         }
                         """, AppJsonSerializerContext.Default.ApiInfoObject) ??
                     new ApiInfo<object>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    ///     发送更新隧道申请
    /// </summary>
    /// <param name="body">请求体</param>
    /// <returns></returns>
    public static async Task<ApiInfo<object>> UpdateTunnelAsync(string body)
    {
        App.CurrentLogger.Log("正在发送更新隧道申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/update");

        var response = await client.ExecuteAsync(request);

        App.CurrentLogger.Log($"状态：{response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonSerializer.Deserialize<ApiInfo<object>>(response.Content ?? """
                         {
                         "code": 0,
                         "message": "无法获取 api 信息",
                         "data": {
                             "users": 0,
                             "nodes": 0,
                             "proxies": 0,
                             "traffic": 0
                             }
                         }
                         """, AppJsonSerializerContext.Default.ApiInfoObject) ??
                     new ApiInfo<object>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    ///     获取用户的隧道列表
    /// </summary>
    /// <returns>一个"用户隧道"数组。</returns>
    public static async Task<ApiInfo<ProxyInfo>> GetProxiesAsync()
    {
        ApiInfo<ProxyInfo> result = null;
        await AppAnalytics.TrackCostAsync("api.proxy.list", async () =>
        {
            result = await ExecuteRequestAsync<ProxyInfo>(CreateRequest(), "auth/proxy/list", "隧道列表");
        });
        return result;
    }

    /// <summary>
    ///     获取用于快速启动的frpToken。
    /// </summary>
    /// <returns></returns>
    public static async Task<ApiInfo<FrpTokenInfo>> GetFrpTokenAsync()
    {
        ApiInfo<FrpTokenInfo> result = null;
        await AppAnalytics.TrackCostAsync("api.proxy.token", async () =>
        {
            result = await ExecuteRequestAsync<FrpTokenInfo>(CreateRequest(), "auth/user/frpToken", "FrpToken信息");
        });
        return result;
    }

    /// <summary>
    ///     获取启动配置
    /// </summary>
    /// <param name="proxyId">要获取的隧道ID</param>
    /// <param name="format">支持的格式: toml, json, yaml, ini</param>
    /// <returns></returns>
    public static ApiInfo<ConfigInfo> GetLaunchConfig(int proxyId, string format)
    {
        App.CurrentLogger.Log("正在发送启动配置申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonSerializer.Serialize(new
        {
            proxyId,
            format
        }, new JsonSerializerOptions { WriteIndented = true });
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/config");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonSerializer.Deserialize<ApiInfo<ConfigInfo>>(response.Content ?? """
                         {
                         "code": 0,
                         "message": "无法获取api信息",
                         "data": {
                             "users": 0,
                             "nodes": 0,
                             "proxies": 0,
                             "traffic": 0
                             }
                         }
                         """, AppJsonSerializerContext.Default.Options) ??
                     new ApiInfo<ConfigInfo>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    ///     切换隧道状态
    /// </summary>
    /// <param name="proxyId">要切换的隧道ID</param>
    /// <param name="isDisabled">是不是要禁用隧道</param>
    /// <returns></returns>
    public static ApiInfo<object> ToggleProxyStatus(int proxyId, bool isDisabled)
    {
        App.CurrentLogger.Log("正在发送切换隧道状态隧道申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonSerializer.Serialize(new
        {
            proxyId,
            isDisabled
        }, new JsonSerializerOptions { WriteIndented = true });
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/toggle");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonSerializer.Deserialize<ApiInfo<object>>(response.Content ?? """
                         {
                         "code": 0,
                         "message": "无法获取api信息",
                         "data": null
                         }
                         """, AppJsonSerializerContext.Default.Options) ??
                     new ApiInfo<object>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    ///     强制下线隧道
    /// </summary>
    /// <param name="proxyId">要下线的隧道ID</param>
    /// <returns></returns>
    public static ApiInfo<object> KickProxy(int proxyId)
    {
        App.CurrentLogger.Log("正在发送强制下线隧道申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonSerializer.Serialize(new
        {
            proxyId
        });
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/kick");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        // 处理可能的多个 JSON 响应
        var content = response.Content?.Trim();
        if (string.IsNullOrEmpty(content))
        {
            return new ApiInfo<object> { code = 500, message = "Empty response" };
        }

        // 检查是否是多个 JSON 对象连在一起
        if (content.StartsWith("{") && content.IndexOf("}{", StringComparison.Ordinal) > 0)
        {
            var firstJsonEnd = content.IndexOf("}{", StringComparison.Ordinal) + 1;
            var firstJson = content[..firstJsonEnd];
            var secondJson = content[firstJsonEnd..];

            try
            {
                var firstResult =
                    JsonSerializer.Deserialize<ApiInfo<object>>(firstJson, AppJsonSerializerContext.Default.Options);
                if (firstResult != null && firstResult.code != 200)
                {
                    HandleResponse(firstResult);
                    return firstResult;
                }
            }
            catch
            {
                /* 忽略解析错误，尝试第二个 */
            }

            try
            {
                var secondResult =
                    JsonSerializer.Deserialize<ApiInfo<object>>(secondJson, AppJsonSerializerContext.Default.Options);
                if (secondResult != null)
                {
                    HandleResponse(secondResult);
                    return secondResult;
                }
            }
            catch
            {
                /* 忽略解析错误 */
            }
        }

        try
        {
            var result =
                JsonSerializer.Deserialize<ApiInfo<object>>(content, AppJsonSerializerContext.Default.Options) ??
                new ApiInfo<object>();
            HandleResponse(result);
            return result;
        }
        catch (JsonException ex)
        {
            App.CurrentLogger.Log($"JSON 解析失败: {ex.Message}", port: EnumLogPort.Server, module: EnumLogModule.Net);
            return new ApiInfo<object> { code = 500, message = "Invalid JSON response" };
        }
    }

    /// <summary>
    ///     删除隧道
    /// </summary>
    /// <param name="proxyId">要删除的隧道ID</param>
    /// <returns></returns>
    public static ApiInfo<object> DeleteProxy(int proxyId)
    {
        App.CurrentLogger.Log("正在发送删除隧道申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonSerializer.Serialize(new
        {
            proxyId
        });
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/delete");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonSerializer.Deserialize<ApiInfo<object>>(response.Content ?? """
                         {
                         "code": 0,
                         "message": "无法获取api信息",
                         "data": {
                             "users": 0,
                             "nodes": 0,
                             "proxies": 0,
                             "traffic": 0
                             }
                         }
                         """, AppJsonSerializerContext.Default.Options) ??
                     new ApiInfo<object>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    ///     初始化方法 - 请不要滥用
    ///     保持兼容的同步 wrapper（会阻塞），并提供异步版本 InitializeAsync
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Initialize()
    {
        // Compatibility wrapper: blockingly run the async initializer if callers expect the old signature.
        InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static async Task InitializeAsync() => CurrentPublicInfo = await GetPublicInfoAsync().ConfigureAwait(false);

    public static async Task PostInitializeAsync()
    {
        if (!UserCache.IsLoggedIn())
        {
            return;
        }

        // If caches are already present, nothing to do
        if (CurrentNodesListInfo != null && CurrentNodesStatusInfo != null)
        {
            return;
        }

        // Ensure both are initialized in a safe way
        var listTask = EnsureNodesListInfoAsync();
        var statusTask = EnsureNodesStatusInfoAsync();

        await Task.WhenAll(listTask, statusTask).ConfigureAwait(false);
    }

    /// <summary>
    ///     异步获取用户的流量统计信息
    /// </summary>
    /// <param name="period">获取的周期，官网上只有 7，15，30</param>
    /// <returns>用户的流量信息</returns>
    public static async Task<ApiInfo<TrafficStatus>> GetTrafficStatusAsync(int period)
    {
        App.CurrentLogger.Log("正在获取流量统计", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonSerializer.Serialize(new
        {
            datePeriod = period
        });
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/user/trafficStats");

        ApiInfo<TrafficStatus> result;
        var response = await client.ExecuteAsync(request).ConfigureAwait(false);

        App.CurrentLogger.Log($"状态：{response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (response.Content?.StartsWith('<') == true)
        {
            return default;
        }

        if (!response.IsSuccessful)
        {
            result = new ApiInfo<TrafficStatus>
            {
                code = (int)response.StatusCode,
                message = $"请求失败: {(int)response.StatusCode}",
                data = default
            };
        }
        else
        {
            result = JsonSerializer.Deserialize<ApiInfo<TrafficStatus>>(response.Content ?? "",
                         AppJsonSerializerContext.Default.ApiInfoTrafficStatus) ??
                     new ApiInfo<TrafficStatus>();
        }

        HandleResponse(result);
        return result;
    }
}