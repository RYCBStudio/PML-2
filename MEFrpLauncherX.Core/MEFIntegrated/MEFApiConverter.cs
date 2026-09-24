using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Services;
using MEFrpLauncherX.Core.Storage;
using MEFrpLauncherX.Core.ViewModels;
using RestSharp;
using RYCB.PML2.MEFrpCaptchaLib;
using static MEFrpLauncherX.Core.MEFIntegrated.InfoClasses;

// ReSharper disable SuspiciousLockOverSynchronizationPrimitive
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8604 // 引用类型参数可能为 null。
#pragma warning disable CS8601 // 引用类型赋值可能为 null。
#pragma warning disable CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
#pragma warning disable CS8603 // 可能返回 null 引用。
#pragma warning disable CS8602 // 解引用可能出现空引用。
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。

namespace MEFrpLauncherX.Core.MEFIntegrated;

public static class MEFrpApiConverter
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

    // 26.4：统一缓存的作用域提供者（只读内存字段，避免每次构造缓存键都读磁盘）
    static MEFrpApiConverter() => ApiCacheService.UserScopeProvider = () => UserCache.CurrentUser?.username;

    /// <summary>
    ///     尝试从统一缓存中取出仍然有效的响应并反序列化（26.4）。
    ///     命中后不再发起网络请求，也不再重复弹提示，保证「缓存期内直接用缓存」。
    /// </summary>
    /// <returns>命中且可反序列化时返回结果，否则返回 null 以触发真实请求。</returns>
    private static ApiInfo<T>? TryGetCached<T>(string? cacheKey, string operationName)
    {
        if (string.IsNullOrEmpty(cacheKey) || !ApiCacheService.TryGetContent(cacheKey, out var cached))
        {
            return null;
        }

        try
        {
            var cachedResult = JsonSerializer.Deserialize<ApiInfo<T>>(cached, App.AppJsonSerializerContext.Options);
            if (cachedResult is null)
            {
                // 缓存内容无法解析（如类型变更）→ 丢弃并回退到网络请求
                ApiCacheService.Invalidate(cacheKey);
                return null;
            }

            App.CurrentLogger.LogDebug($"[缓存命中] {cacheKey}（5 分钟内不重复请求）",
                port: EnumLogPort.Client, module: EnumLogModule.Net);
            MainWindowViewModel.Instance?.AppMessage =
                string.Format(Languages.Languages.Text_Api_CacheHitFormat, operationName);
            return cachedResult;
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, $"读取 API 缓存失败: {cacheKey}");
            ApiCacheService.Invalidate(cacheKey);
            return null;
        }
    }

    private static async Task<ApiInfo<T>> ExecuteRequestAsync<T>(RestRequest request, string endpoint,
        string operationName, string? cacheKey = null, bool forceRefresh = false, bool cacheEmptyData = false)
    {
        // 26.4：缓存优先。页面展示类数据在 5 分钟有效期内直接复用上次成功请求的结果；
        //        forceRefresh（用户显式刷新 / 写操作后）与未配置键的接口始终走网络。
        if (!forceRefresh && TryGetCached<T>(cacheKey, operationName) is { } cached)
        {
            return cached;
        }

        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + endpoint}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger.Log($"正在获取{operationName}", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = string.Format(Languages.Languages.Text_Api_FetchingFormat, operationName);

        using var client = CreateClient(endpoint);

        var response = await client.ExecuteAsync(request).ConfigureAwait(false);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        if (string.IsNullOrEmpty(response.Content))
        {
            var fallBack = new ApiInfo<T>
            {
                code = 0,
                message = Languages.Languages.Text_Api_CannotGetApiInfo,
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
                message = Languages.Languages.Text_Api_OriginFallbackFailed,
                data = default
            };
        }

        var result =
            JsonSerializer.Deserialize<ApiInfo<T>>(response.Content ?? "", App.AppJsonSerializerContext.Options) ??
            new ApiInfo<T>
            {
                code = 0,
                message = Languages.Languages.Text_Api_CannotGetApiInfo,
                data = default
            };

        // 26.4：仅缓存成功结果；失败/异常响应不写入，避免把错误状态固化 5 分钟。
        //       cacheEmptyData 用于「空数据本身即为合法结果」的接口（如无弹窗公告）。
        if (!string.IsNullOrEmpty(cacheKey))
        {
            if (result.code == 200 && (cacheEmptyData || result.data is not null))
            {
                ApiCacheService.SetContent(cacheKey, response.Content!);
            }
            else
            {
                ApiCacheService.Invalidate(cacheKey);
            }
        }

        HandleResponse(result);
        MainWindowViewModel.Instance?.AppMessage = string.Format(Languages.Languages.Text_Api_DoneCodeFormat, result.code);
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
            App.AppJsonSerializerContext.ChallengeInfo)!;
    }

    public static async Task<(CaptchaResultX?, string)> GetRedeemAsync(string redeemBody)
    {
        App.CurrentLogger.Log("Sending Captcha Challenge Request", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        request.AddParameter("application/json", redeemBody, ParameterType.RequestBody);
        using var client = new RestClient(Constants.RedeemUrl);
        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (!response.IsSuccessful)
        {
            return (null, string.Empty);
        }
        return (
            JsonSerializer.Deserialize<CaptchaResultX?>(response.Content ?? "",
                App.AppJsonSerializerContext.CaptchaResultX), response.Content ?? "");
    }

    /// <summary>
    ///    获取ICP备案域名列表
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求</param>
    /// <returns></returns>
    public static async Task<ApiInfo<List<IcpDomain>>> GetIcpDomainListAsync(bool forceRefresh = false)
    {
        ApiInfo<List<IcpDomain>> result = null;
        await AppAnalytics.TrackCostAsync("api.icp.domain-list", async () =>
        {
            result = await ExecuteRequestAsync<List<IcpDomain>>(CreateRequest(), "auth/user/icpDomain/list",
                Languages.Languages.Text_Api_OpIcpDomainList, ApiCacheKeys.IcpDomainList, forceRefresh);
        });
        return result;
    }

    public static async Task<ApiInfo<object>> DeleteIcpDomainAsync(string domain)
    {
        var request = CreateRequest(Method.Post);
        var body = JsonSerializer.Serialize(new ToEditIcpDomainInfo
        {
            domain = domain
        }, App.AppJsonSerializerContext.ToEditIcpDomainInfo);
        request.AddParameter("application/json", body, ParameterType.RequestBody);
        using var client = CreateClient("auth/user/icpDomain/delete");

        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态：{response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        var result = JsonSerializer.Deserialize<ApiInfo<object>>(response.Content ?? "",
            App.AppJsonSerializerContext.ApiInfoObject);

        // 26.4：写操作成功后失效备案域名缓存，保证下次读到的列表是最新的
        if (result?.code == 200)
        {
            ApiCacheService.Invalidate(ApiCacheKeys.IcpDomainList);
        }

        return result;
    }
    
    public static async Task<ApiInfo<object>> AddIcpDomainAsync(string domain)
    {
        var request = CreateRequest(Method.Post);
        request.Timeout = TimeSpan.FromSeconds(6); // 接口延迟较大, 需要延长超时时间
        var body = JsonSerializer.Serialize(new ToEditIcpDomainInfo
        {
            domain = domain
        }, App.AppJsonSerializerContext.ToEditIcpDomainInfo);
        request.AddParameter("application/json", body, ParameterType.RequestBody);
        using var client = CreateClient("auth/user/icpDomain/add");

        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态：{response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        var result = JsonSerializer.Deserialize<ApiInfo<object>>(response.Content ?? "",
            App.AppJsonSerializerContext.ApiInfoObject);

        // 26.4：写操作成功后失效备案域名缓存
        if (result?.code == 200)
        {
            ApiCacheService.Invalidate(ApiCacheKeys.IcpDomainList);
        }

        return result;
    }

    /// <summary>
    ///     获取系统状态
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求（用户显式刷新时使用）</param>
    /// <returns></returns>
    public static async Task<ApiInfo<SystemStatus?>> GetSystemStatusAsync(bool forceRefresh = false)
    {
        ApiInfo<SystemStatus?> result = null;
        await AppAnalytics.TrackCostAsync("api.system.status", async () =>
        {
            result = await ExecuteRequestAsync<SystemStatus?>(CreateRequest(), "auth/system/status",
                    Languages.Languages.Text_Api_OpSystemStatus, ApiCacheKeys.SystemStatus, forceRefresh)
                .ConfigureAwait(false);
        });
        return result;
    }

    /// <summary>
    ///     获取重要公告
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求</param>
    /// <returns></returns>
    public static async Task<ApiInfo<string?>> GetPopupNoticeAsync(bool forceRefresh = false)
    {
        // 26.4：「无弹窗公告」是合法结果（data 为 null），因此允许缓存空数据
        var result = await ExecuteRequestAsync<string?>(CreateRequest(), "auth/popupNotice",
            Languages.Languages.Text_Api_OpPopupNotice, ApiCacheKeys.PopupNotice, forceRefresh,
            cacheEmptyData: true);
        return result;
    }

    /// <summary>
    ///     异步获取公告
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求</param>
    /// <returns>公告内容</returns>
    public static async Task<ApiInfo<string>> GetNoticeAsync(bool forceRefresh = false)
    {
        return await ExecuteRequestAsync<string>(CreateRequest(), "auth/notice",
            Languages.Languages.Text_Api_OpNotice, ApiCacheKeys.Notice, forceRefresh,
            cacheEmptyData: true);
    }

    /// <summary>
    ///     获取公共信息
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求</param>
    /// <returns>公共信息</returns>
    public static async Task<ApiInfo<PublicData>> GetPublicInfoAsync(bool forceRefresh = false)
    {
        var result = await ExecuteRequestAsync<PublicData>(CreateRequest(), "public/statistics",
            Languages.Languages.Text_Api_OpPublicInfo, ApiCacheKeys.PublicInfo, forceRefresh);
        CurrentPublicInfo = result;
        return result;
    }

    /// <summary>
    ///     异步获取用户信息
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求（签到后用户中心刷新使用）</param>
    /// <returns>用户信息</returns>
    public static async Task<ApiInfo<ExtraUserInfo>> GetExtraUserInfoAsync(bool forceRefresh = false)
    {
        ApiInfo<ExtraUserInfo> result = null;
        await AppAnalytics.TrackCostAsync("api.user-info", async () =>
        {
            result = await ExecuteRequestAsync<ExtraUserInfo>(CreateRequest(), "auth/user/info",
                Languages.Languages.Text_Api_OpExtraUserInfo, ApiCacheKeys.UserInfo, forceRefresh);
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
    ///     发送签到请求 (异步)
    /// </summary>
    /// <param name="code">人机验证码</param>
    /// <returns>(是否成功，返回的 response 内容)</returns>
    public static async Task<(bool, string?)> SendSignRequestAsync(string code)
    {
        App.CurrentLogger.Log("正在发送签到请求", port: EnumLogPort.Client, module: EnumLogModule.Net);
        var request = CreateRequest(Method.Post);
        var cr = GetCaptchaResult(code);
        var body = JsonSerializer.Serialize(new SignPost
        {
            captchaToken = cr
        }, App.AppJsonSerializerContext.SignPost);
        request.AddParameter("application/json", body, ParameterType.RequestBody);
        using var client = CreateClient("auth/user/sign");

        var response = await client.ExecuteAsync(request);
        App.CurrentLogger.Log($"状态：{response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        (bool, string?) result = (response.Content?.Contains("成功") ?? false, response.Content);

        // 26.4：签到会改变账户流量/签到态等展示数据 → 失效用户信息与流量统计缓存
        if (result.Item1)
        {
            ApiCacheService.Invalidate(ApiCacheKeys.UserInfo);
            ApiCacheService.InvalidatePrefix(ApiCacheKeys.TrafficStatsPrefix);
        }

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
        }, App.AppJsonSerializerContext.LoginInfo);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("public/login");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        return (response.Content?.Contains("成功") ?? false, response.Content);
    }

    /// <summary>
    ///     获取节点状态
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求（用户点「刷新」时使用）</param>
    /// <returns>一个"单个节点状态"数组。</returns>
    public static async Task<ApiInfo<NodeStatus[]>> GetNodesStatusAsync(bool forceRefresh = false)
    {
        ApiInfo<NodeStatus[]> result = null;
        await AppAnalytics.TrackCostAsync("api.nodes.status", async () =>
        {
            result = await ExecuteRequestAsync<NodeStatus[]>(CreateRequest(), "auth/node/status",
                Languages.Languages.Text_Api_OpNodeStatus, ApiCacheKeys.NodesStatus, forceRefresh);
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
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求</param>
    /// <returns>一个"单个节点信息"数组。</returns>
    public static async Task<ApiInfo<NodeInfo[]>> GetNodesInfoAsync(bool forceRefresh = false)
    {
        ApiInfo<NodeInfo[]> result = null;
        await AppAnalytics.TrackCostAsync("api.nodes.info", async () =>
        {
            result = await ExecuteRequestAsync<NodeInfo[]>(CreateRequest(), "auth/node/list",
                Languages.Languages.Text_Api_OpNodeInfo, ApiCacheKeys.NodesInfo, forceRefresh);
        });
        return result;
    }

    /// <summary>
    ///     获取已创建隧道的节点连接地址
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求</param>
    /// <returns></returns>
    public static async Task<ApiInfo<NodeNameList[]>> GetNodesNameListAsync(bool forceRefresh = false)
    {
        ApiInfo<NodeNameList[]> result = null;
        await AppAnalytics.TrackCostAsync("api.nodes.name-list", async () =>
        {
            result = await ExecuteRequestAsync<NodeNameList[]>(CreateRequest(), "auth/node/nameList",
                Languages.Languages.Text_Api_OpConnectedNodeInfo, ApiCacheKeys.NodesNameList, forceRefresh);
        });
        return result;
    }

    /// <summary>
    ///     确保节点列表缓存已初始化（线程安全，幂等）。
    ///     <para>
    ///         26.4：进程内对象缓存与统一响应缓存共用同一 TTL——超过 5 分钟后重新请求，
    ///         保证与「所有页面统一 5 分钟缓存」的策略一致（此前该内存缓存永不过期）。
    ///     </para>
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="forceRefresh">true 表示忽略内存缓存与 5 分钟响应缓存、强制重新请求</param>
    public static async Task<NodesListInfo?> EnsureNodesListInfoAsync(CancellationToken cancellationToken = default,
        bool forceRefresh = false)
    {
        // Fast path：仅当统一缓存中的节点列表仍在有效期内，才复用进程内对象
        if (!forceRefresh && Volatile.Read(ref _nodesListInfo) != null &&
            ApiCacheService.IsFresh(ApiCacheKeys.NodesInfo))
        {
            return _nodesListInfo;
        }

        if (forceRefresh)
        {
            Volatile.Write(ref _nodesListInfo, null);
        }

        await _nodesListSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _nodesListInfo != null && ApiCacheService.IsFresh(ApiCacheKeys.NodesInfo))
            {
                return _nodesListInfo;
            }

            var apiResult = await GetNodesInfoAsync(forceRefresh);
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
    ///     确保节点状态缓存已初始化（线程安全，幂等）。
    ///     <para>
    ///         26.4：进程内对象缓存与统一响应缓存共用同一 TTL——超过 5 分钟后重新请求，
    ///         保证节点监控 / 创建隧道页展示的数据同样遵循统一缓存策略。
    ///     </para>
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="forceRefresh">true 表示忽略内存缓存与 5 分钟响应缓存、强制重新请求</param>
    public static async Task<NodesStatusInfo?> EnsureNodesStatusInfoAsync(CancellationToken cancellationToken = default,
        bool forceRefresh = false)
    {
        if (!forceRefresh && Volatile.Read(ref _nodesStatusInfo) != null &&
            ApiCacheService.IsFresh(ApiCacheKeys.NodesStatus))
        {
            return _nodesStatusInfo;
        }

        if (forceRefresh)
        {
            Volatile.Write(ref _nodesStatusInfo, null);
        }

        await _nodesStatusSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _nodesStatusInfo != null && ApiCacheService.IsFresh(ApiCacheKeys.NodesStatus))
            {
                return _nodesStatusInfo;
            }

            var apiResult = await GetNodesStatusAsync(forceRefresh).ConfigureAwait(false);
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
        }, App.AppJsonSerializerContext.FreePortBody);

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
            """, App.AppJsonSerializerContext.ApiInfoInt32) ?? new ApiInfo<int> { data = -1 };
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
                         """, App.AppJsonSerializerContext.ApiInfoObject) ??
                     new ApiInfo<object>();
        HandleResponse(result);

        // 26.4：新建隧道属于写操作 → 失效隧道列表缓存，避免管理页在 5 分钟内看不到新隧道
        InvalidateProxyListOnSuccess(result);
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
                         """, App.AppJsonSerializerContext.ApiInfoObject) ??
                     new ApiInfo<object>();
        HandleResponse(result);

        // 26.4：更新隧道属于写操作 → 失效隧道列表缓存
        InvalidateProxyListOnSuccess(result);
        return result;
    }

    /// <summary>
    ///     隧道列表相关写操作成功后统一失效缓存（26.4）。
    ///     仅在 <c>code == 200</c> 时失效，失败的写操作不改动缓存。
    /// </summary>
    private static void InvalidateProxyListOnSuccess(ApiInfo<object>? result)
    {
        if (result?.code == 200)
        {
            ApiCacheService.Invalidate(ApiCacheKeys.ProxyList);
        }
    }

    /// <summary>
    ///     获取用户的隧道列表
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求（管理页显式刷新时使用）</param>
    /// <returns>一个"用户隧道"数组。</returns>
    public static async Task<ApiInfo<ProxyInfo>> GetProxiesAsync(bool forceRefresh = false)
    {
        ApiInfo<ProxyInfo> result = null;
        await AppAnalytics.TrackCostAsync("api.proxy.list", async () =>
        {
            result = await ExecuteRequestAsync<ProxyInfo>(CreateRequest(), "auth/proxy/list",
                Languages.Languages.Text_Api_OpProxyList, ApiCacheKeys.ProxyList, forceRefresh);
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
            result = await ExecuteRequestAsync<FrpTokenInfo>(CreateRequest(), "auth/user/frpToken", Languages.Languages.Text_Api_OpFrpTokenInfo);
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
        var body = JsonSerializer.Serialize(new LaunchConfigRequest
        {
            proxyId = proxyId,
            format = format
        }, App.AppJsonSerializerContext.LaunchConfigRequest);
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
                         """, App.AppJsonSerializerContext.ApiInfoConfigInfo) ??
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
        var body = JsonSerializer.Serialize(new ToggleProxyInfo
        {
            proxyId = proxyId,
            isDisabled = isDisabled
        }, App.AppJsonSerializerContext.ToggleProxyInfo);
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
                         """, App.AppJsonSerializerContext.ApiInfoObject) ??
                     new ApiInfo<object>();
        HandleResponse(result);

        // 26.4：切换隧道状态属于写操作 → 失效隧道列表缓存
        InvalidateProxyListOnSuccess(result);
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
        var body = JsonSerializer.Serialize(new KickProxyInfo
        {
            proxyId = proxyId
        }, App.AppJsonSerializerContext.KickProxyInfo);
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
                    JsonSerializer.Deserialize<ApiInfo<object>>(firstJson, App.AppJsonSerializerContext.ApiInfoObject);
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
                    JsonSerializer.Deserialize<ApiInfo<object>>(secondJson, App.AppJsonSerializerContext.ApiInfoObject);
                if (secondResult != null)
                {
                    HandleResponse(secondResult);
                    // 26.4：强制下线属于写操作 → 失效隧道列表缓存
                    InvalidateProxyListOnSuccess(secondResult);
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
                JsonSerializer.Deserialize<ApiInfo<object>>(content, App.AppJsonSerializerContext.ApiInfoObject) ??
                new ApiInfo<object>();
            HandleResponse(result);
            // 26.4：强制下线属于写操作 → 失效隧道列表缓存
            InvalidateProxyListOnSuccess(result);
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
        var body = JsonSerializer.Serialize(new DeleteProxyInfo
        {
            proxyId = proxyId
        }, App.AppJsonSerializerContext.DeleteProxyInfo);
        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("auth/proxy/delete");
        var response = client.Execute(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result = JsonSerializer.Deserialize<ApiInfo<object>>(response.Content ?? """
                         {
                         "code": 0,
                         "message": "无法获取api信息",
                         "data": null
                         }
                         """, App.AppJsonSerializerContext.ApiInfoObject) ??
                     new ApiInfo<object>();
        HandleResponse(result);

        // 26.4：删除隧道属于写操作 → 失效隧道列表缓存
        InvalidateProxyListOnSuccess(result);
        return result;
    }

    /// <summary>
    ///     清空进程内的节点缓存（26.4）。登录 / 退出登录时调用，
    ///     避免切换账号后复用上一位用户的节点列表与状态。
    /// </summary>
    public static void ResetInMemoryCaches()
    {
        Volatile.Write(ref _nodesListInfo, null);
        Volatile.Write(ref _nodesStatusInfo, null);
    }

    /// <summary>
    ///     刷新用户相关数据的统一缓存（26.4）。
    ///     写操作（如签到、资料变更）后调用，使下一次读取拿到最新结果。
    /// </summary>
    public static void InvalidateUserRelatedCaches()
    {
        ApiCacheService.Invalidate(ApiCacheKeys.UserInfo);
        ApiCacheService.InvalidatePrefix(ApiCacheKeys.TrafficStatsPrefix);
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
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存、强制重新请求</param>
    /// <returns>用户的流量信息</returns>
    public static async Task<ApiInfo<TrafficStatus>> GetTrafficStatusAsync(int period, bool forceRefresh = false)
    {
        // 26.4：不同周期各自独立缓存（键带周期），在 5 分钟有效期内直接复用上次结果
        var cacheKey = $"{ApiCacheKeys.TrafficStatsPrefix}{period}";
        if (!forceRefresh && TryGetCached<TrafficStatus>(cacheKey, Languages.Languages.Text_Api_OpTrafficStatus)
            is { } cachedTraffic)
        {
            return cachedTraffic;
        }

        App.CurrentLogger.Log("正在获取流量统计", module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);
        var body = JsonSerializer.Serialize(new DatePeriod
        {
            datePeriod = period
        }, App.AppJsonSerializerContext.DatePeriod);
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
                message = string.Format(Languages.Languages.Text_Api_RequestFailedFormat, (int)response.StatusCode),
                data = default
            };
        }
        else
        {
            result = JsonSerializer.Deserialize<ApiInfo<TrafficStatus>>(response.Content ?? "",
                         App.AppJsonSerializerContext.ApiInfoTrafficStatus) ??
                     new ApiInfo<TrafficStatus>();
        }

        // 26.4：仅成功结果写入缓存（失败不缓存，避免把错误状态固化 5 分钟）
        if (result is { code: 200, data: not null })
        {
            ApiCacheService.SetContent(cacheKey, response.Content!);
        }
        else
        {
            ApiCacheService.Invalidate(cacheKey);
        }

        HandleResponse(result);
        return result;
    }
}