using System.ComponentModel;
using System.Net;
using System.Text;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Storage;
using Newtonsoft.Json;
using RestSharp;
using RYCB.PML.MEFrpCaptchaLib;
using static MEFrpLauncherX.Core.MEFIntergrated.InfoClasses;

namespace MEFrpLauncherX.Core.MEFIntergrated;

public class MEFApiConverter
{
    public const string BaseApiUrl = "https://api.mefrp.com/api/";

    public static RestClient? CurrentClient
    {
        get;
        set;
    }

    /// <summary>
    /// 当前的公共信息
    /// </summary>
    public static ApiInfo<PublicData> CurrentPublicInfo
    {
        get;
        private set;
    } = new();

    /// <summary>
    /// 当前的用户信息
    /// </summary>
    public static ApiInfo<UserInfo> CurrentUserInfo
    {
        get;
        set;
    }

    // Backing fields for node infos (cached)
    private static NodesListInfo? _nodesListInfo;
    private static NodesStatusInfo? _nodesStatusInfo;

    // SemaphoreSlim for async initialization to prevent concurrent initializations
    private static readonly SemaphoreSlim _nodesListSemaphore = new(1, 1);
    private static readonly SemaphoreSlim _nodesStatusSemaphore = new(1, 1);

    /// <summary>
    /// 当前的节点List信息（仅返回缓存，不触发网络请求）
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
    /// 当前的节点状态信息（仅返回缓存，不触发网络请求）
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
            UserAgent = OperatingSystem.IsAndroid() ? "RYCB-PML2/Android 0.0.1" : "RYCB-PML2/Desktop 2.1.0",
            Timeout = TimeSpan.FromSeconds(3),
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
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + endpoint}", port: EnumLogPort.Server,
            module: EnumLogModule.Custom, customModuleName: "API");
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
            return new ApiInfo<T>()
            {
                code = 502,
                message = "API回源失败, 无法获取api信息",
                data = default
            };
        }
        
        var result = JsonConvert.DeserializeObject<ApiInfo<T>>(response.Content ?? """
            {
            "code": 0,
            "message": "无法获取api信息",
            "data": null
            }
            """) ?? new ApiInfo<T>
        {
            code = 0,
            message = "无法获取api信息",
            data = default
        };

        HandleResponse(result);
        MainWindowViewModel.Instance?.AppMessage = $"完成, 返回代码: {result.code}";
        return result;
    }

    private static ApiInfo<T> ExecuteRequest<T>(RestRequest request, string endpoint, string operationName)
    {
        // Keep existing behavior: if not logged in and not requesting public info, return failure
        if (!UserCache.IsLoggedIn() && operationName != "公共信息")
        {
            return new ApiInfo<T>
            {
                data = default,
                message = "无法获取api信息",
                code = 0
            };
        }

        App.CurrentLogger.Log($"正在获取{operationName}", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = $"正在获取{operationName}";

        using var client = CreateClient(endpoint);
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        if (response.Content.StartsWith("<"))
        {
            return new ApiInfo<T>()
            {
                code = 502,
                message = "API回源失败, 无法获取api信息",
                data = default
            };
        }
        // Fallback to safe default JSON if content is null
        var safeContent = response.Content ?? """
                                              {
                                              "code": 0,
                                              "message": "无法获取api信息",
                                              "data": null
                                              }
                                              """;

        var result = JsonConvert.DeserializeObject<ApiInfo<T>>(safeContent) ?? new ApiInfo<T>
        {
            data = default,
            message = "无法获取api信息",
            code = 0
        };

        HandleResponse(result);
        MainWindowViewModel.Instance?.AppMessage = $"完成, 返回代码: {result.code}";
        return result;
    }

    public static async Task<ChallengeInfo> PostChallengeAsync(string challengeContent)
    {
        App.CurrentLogger.Log("Sending Captcha Challenge Request", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        var body = challengeContent;
        request.AddParameter("application/json", body, ParameterType.RequestBody);
        using var client = new RestClient(Constants.ChallengeUrl);
        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return JsonConvert.DeserializeObject<ChallengeInfo>(response.Content ?? "")!;
    }

    public static async Task<(CaptchaResultX, string)> GetRedeemAsync(string redeemBody)
    {
        App.CurrentLogger.Log("Sending Captcha Challenge Request", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        request.AddParameter("application/json", redeemBody, ParameterType.RequestBody);
        using var client = new RestClient(Constants.RedeemUrl);
        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return (JsonConvert.DeserializeObject<CaptchaResultX>(response.Content ?? "")!, response.Content ?? "");
    }

    /// <summary>
    /// 获取系统状态
    /// </summary>
    /// <returns></returns>
    public static async Task<ApiInfo<SystemStatus?>> GetSystemStatusAsync()
    {
        return await ExecuteRequestAsync<SystemStatus?>(CreateRequest(), "auth/system/status", "系统状态")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 获取重要公告
    /// </summary>
    /// <returns></returns>
    public static async Task<ApiInfo<string?>> GetPopupNoticeAsync()
    {
        return await ExecuteRequestAsync<string?>(CreateRequest(), "auth/popupNotice", "重要公告");
    }

    /// <summary>
    /// 异步获取公告
    /// </summary>
    /// <returns>公告内容</returns>
    public static Task<ApiInfo<string>> GetNoticeAsync()
    {
        return ExecuteRequestAsync<string>(CreateRequest(), "auth/notice", "公告");
    }

    /// <summary>
    /// 获取公共信息
    /// </summary>
    /// <returns>公共信息</returns>
    public static async Task<ApiInfo<PublicData>> GetPublicInfoAsync()
    {
        var result = await ExecuteRequestAsync<PublicData>(CreateRequest(), "public/statistics", "公共信息");
        CurrentPublicInfo = result;
        return result;
    }

    /// <summary>
    /// 异步获取用户信息
    /// </summary>
    /// <returns>用户信息</returns>
    public static Task<ApiInfo<ExtraUserInfo>> GetExtraUserInfoAsync()
    {
        return ExecuteRequestAsync<ExtraUserInfo>(CreateRequest(), "auth/user/info", "详细的用户信息");
    }

    /// <summary>
    /// 获取用户信息
    /// </summary>
    /// <returns>用户信息</returns>
    public static ApiInfo<ExtraUserInfo> GetExtraUserInfo()
    {
        return ExecuteRequest<ExtraUserInfo>(CreateRequest(), "auth/user/info", "详细的用户信息");
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
    /// 发送签到请求
    /// </summary>
    /// <param name="code">人机验证码</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static (bool, string?) SendSignRequest(string code)
    {
        App.CurrentLogger.Log("正在发送签到请求", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        var cr = GetCaptchaResult(code);
        var body = JsonConvert.SerializeObject(new
        {
            captchaToken = cr
        }, Formatting.Indented);
        request.AddParameter("application/json", body, ParameterType.RequestBody);
        using var client = CreateClient("auth/user/sign");
        var response = client.Execute(request);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return (response.Content?.Contains("成功") ?? false, response.Content);
    }

    /// <summary>
    /// 发送签到请求(异步)
    /// </summary>
    /// <param name="code">人机验证码</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static async Task<(bool, string?)> SendSignRequestAsync(string code)
    {
        App.CurrentLogger.Log("正在发送签到请求", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        var cr = GetCaptchaResult(code);
        var body = JsonConvert.SerializeObject(new
        {
            captchaToken = cr
        }, Formatting.Indented);
        request.AddParameter("application/json", body, ParameterType.RequestBody);
        using var client = CreateClient("auth/user/sign");
        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return (response.Content?.Contains("成功") ?? false, response.Content);
    }

    /// <summary>
    /// 发送登录请求
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="captchaCode">人机验证码</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static (bool, string?) SendLoginInfo(string username, string password, string captchaCode)
    {
        App.CurrentLogger.Log("正在发送登录请求", port: EnumLogPort.Client, module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);

        var body = JsonConvert.SerializeObject(new LoginInfo
        {
            username = username,
            password = password,
            captchaToken = GetCaptchaResult(captchaCode)
        }, Formatting.Indented);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("public/login");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return (response.Content?.Contains("成功") ?? false, response.Content);
    }

    /// <summary>
    /// 获取节点状态
    /// </summary>
    /// <returns>一个“单个节点状态”数组。</returns>
    public static async Task<ApiInfo<NodeStatus[]>> GetNodesStatusAsync()
    {
        var ns = await ExecuteRequestAsync<NodeStatus[]>(CreateRequest(), "auth/node/status", "节点状态")
            .ConfigureAwait(false);

        if (ns is { data: not null })
        {
            // 存储到缓存，线程安全
            lock (_nodesStatusSemaphore)
            {
                _nodesStatusInfo ??= new NodesStatusInfo();
                _nodesStatusInfo.NodesStatus = ns.data;
            }
        }

        return ns;
    }

    /// <summary>
    /// 获取节点信息
    /// </summary>
    /// <returns>一个“单个节点信息”数组。</returns>
    public static async Task<ApiInfo<NodesList[]>> GetNodesInfoAsync()
    {
        return await ExecuteRequestAsync<NodesList[]>(CreateRequest(), "auth/node/list", "节点信息").ConfigureAwait(false);
    }

    /// <summary>
    /// 获取已创建隧道的节点连接地址
    /// </summary>
    /// <returns></returns>
    public static async Task<ApiInfo<NodeNameList[]>> GetNodesNameListAsync()
    {
        return await ExecuteRequestAsync<NodeNameList[]>(CreateRequest(), "auth/node/nameList", "已连接节点信息");
    }

    /// <summary>
    /// 确保节点列表缓存已初始化（线程安全，幂等）
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
    /// 确保节点状态缓存已初始化（线程安全，幂等）
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
    /// 获取空闲端口
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <param name="protocol">要获取端口的协议, 只有tcp和udp。</param>
    /// <returns>空闲的端口，返回-1则说明获取失败</returns>
    public static ApiInfo<int> GetFreePort(int nodeId, string protocol = "tcp")
    {
        App.CurrentLogger.Log("正在获取空闲端口", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        protocol = protocol.ToLower();

        var body = JsonConvert.SerializeObject(new
        {
            nodeId,
            protocol,
        }, Formatting.Indented);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/node/freePort");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonConvert.DeserializeObject<ApiInfo<int>>(response.Content ?? """
            {
            "code": 0,
            "message": "无法获取api信息",
            "data": -1
            }
            """) ?? new ApiInfo<int> { data = -1 };
        HandleResponse(result);
        return result;
    }

    /// <summary>
    /// 发送新建隧道请求
    /// </summary>
    /// <param name="body">要传入的请求体，详见<a href="https://apidoc.mefrp.com"/></param>
    /// <returns></returns>
    public static async Task<ApiInfo<object>> PostNewTunnelAsync(string body)
    {
        App.CurrentLogger.Log("正在发送新建隧道申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/create");
        var response = await client.ExecuteAsync(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonConvert.DeserializeObject<ApiInfo<object>>(response.Content ?? """
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
                         """) ??
                     new ApiInfo<object>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    /// 发送更新隧道申请
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

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonConvert.DeserializeObject<ApiInfo<object>>(response.Content ?? """
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
                         """) ??
                     new ApiInfo<object>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    /// 获取用户的隧道列表
    /// </summary>
    /// <returns>一个"用户隧道"数组。</returns>
    public static async Task<ApiInfo<ProxyInfo>> GetProxiesAsync()
    {
        return await ExecuteRequestAsync<ProxyInfo>(CreateRequest(), "auth/proxy/list", "隧道列表");
    }

    /// <summary>
    /// 获取用于快速启动的frpToken。
    /// </summary>
    /// <returns></returns>
    public static ApiInfo<FrpTokenInfo> GetFrpToken()
    {
        return ExecuteRequest<FrpTokenInfo>(CreateRequest(), "auth/user/frpToken", "FrpToken信息");
    }

    /// <summary>
    /// 获取启动配置
    /// </summary>
    /// <param name="proxyId">要获取的隧道ID</param>
    /// <param name="format">支持的格式: toml, json, yaml, ini</param>
    /// <returns></returns>
    public static ApiInfo<ConfigInfo> GetLaunchConfig(int proxyId, string format)
    {
        App.CurrentLogger.Log("正在发送启动配置申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonConvert.SerializeObject(new
        {
            proxyId,
            format
        }, Formatting.Indented);
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/config");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonConvert.DeserializeObject<ApiInfo<ConfigInfo>>(response.Content ?? """
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
                         """) ??
                     new ApiInfo<ConfigInfo>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    /// 切换隧道状态
    /// </summary>
    /// <param name="proxyId">要切换的隧道ID</param>
    /// <param name="isDisabled">是不是要禁用隧道</param>
    /// <returns></returns>
    public static ApiInfo<object> ToggleProxyStatus(int proxyId, bool isDisabled)
    {
        App.CurrentLogger.Log("正在发送切换隧道状态隧道申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonConvert.SerializeObject(new
        {
            proxyId,
            isDisabled
        }, Formatting.Indented);
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/toggle");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonConvert.DeserializeObject<ApiInfo<object>>(response.Content ?? """
                         {
                         "code": 0,
                         "message": "无法获取api信息",
                         "data": null
                         }
                         """) ??
                     new ApiInfo<object>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    /// 强制下线隧道
    /// </summary>
    /// <param name="proxyId">要下线的隧道ID</param>
    /// <returns></returns>
    public static ApiInfo<object> KickProxy(int proxyId)
    {
        App.CurrentLogger.Log("正在发送强制下线隧道申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonConvert.SerializeObject(new
        {
            proxyId
        }, Formatting.Indented);
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
                var firstResult = JsonConvert.DeserializeObject<ApiInfo<object>>(firstJson);
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
                var secondResult = JsonConvert.DeserializeObject<ApiInfo<object>>(secondJson);
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
            var result = JsonConvert.DeserializeObject<ApiInfo<object>>(content) ?? new ApiInfo<object>();
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
    /// 删除隧道
    /// </summary>
    /// <param name="proxyId">要删除的隧道ID</param>
    /// <returns></returns>
    public static ApiInfo<object> DeleteProxy(int proxyId)
    {
        App.CurrentLogger.Log("正在发送删除隧道申请", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonConvert.SerializeObject(new
        {
            proxyId
        });
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/delete");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonConvert.DeserializeObject<ApiInfo<object>>(response.Content ?? """
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
                         """) ??
                     new ApiInfo<object>();
        HandleResponse(result);
        return result;
    }

    /// <summary>
    /// 初始化方法 - 请不要滥用
    /// 保持兼容的同步 wrapper（会阻塞），并提供异步版本 InitializeAsync
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Initialize()
    {
        // Compatibility wrapper: blockingly run the async initializer if callers expect the old signature.
        InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static async Task InitializeAsync()
    {
        CurrentPublicInfo = await GetPublicInfoAsync().ConfigureAwait(false);
    }

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
    /// 异步获取用户的流量统计信息
    /// </summary>
    /// <param name="period">获取的周期，官网上只有7，15，30</param>
    /// <returns>用户的流量信息</returns>
    public static async Task<ApiInfo<TrafficStatus>> GetTrafficStatusAsync(int period)
    {
        App.CurrentLogger.Log("正在获取流量统计", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonConvert.SerializeObject(new
        {
            datePeriod = period
        });
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/user/trafficStats");
        var response = await client.ExecuteAsync(request).ConfigureAwait(false);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonConvert.DeserializeObject<ApiInfo<TrafficStatus>>(response.Content ?? "") ??
                     new ApiInfo<TrafficStatus>();
        HandleResponse(result);
        return result;
    }
}