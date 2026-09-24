using System.Net;
using System.Reactive;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Services;
using MEFrpLauncherX.Core.ViewModels;
using ReactiveUI;
using RestSharp;
// ReSharper disable InconsistentNaming

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。
#pragma warning disable CS8603 // 可能返回 null 引用。

namespace MEFrpLauncherX.Core;

public static class RYCBApiConverter
{
    private const string BaseApiUrl = "https://api.rycb.tech/api/";

    public static async Task<bool> InitializeAsync()
    {
        App.CurrentLogger?.Log("正在初始化API客户端", port: EnumLogPort.Client, module: EnumLogModule.Net);
        // 消除 CS1998 警告（此异步方法缺少 await 运算符）
        await Task.CompletedTask;
        App.CurrentLogger?.Log("API客户端初始化完成", port: EnumLogPort.Client, module: EnumLogModule.Net);
        return true;
    }

    private static RestRequest CreateRequest(Method method = Method.Get)
    {
        var request = new RestRequest { Method = method };
        if (method != Method.Get)
        {
            request.AddHeader("Content-Type", "application/json");
        }

        return request;
    }

    // ==================== 26.4 统一缓存 ====================

    /// <summary>
    ///     尝试从统一缓存取出仍然有效的内容（26.4）。未命中或无法反序列化时返回 null，
    ///     调用方据此走网络请求。
    /// </summary>
    private static T? TryGetCached<T>(string cacheKey, string operationName, Func<string, T?> deserialize)
        where T : class
    {
        if (!ApiCacheService.TryGetContent(cacheKey, out var cached))
        {
            return null;
        }

        try
        {
            var cachedResult = deserialize(cached);
            if (cachedResult is null)
            {
                ApiCacheService.Invalidate(cacheKey);
                return null;
            }

            App.CurrentLogger?.LogDebug($"[缓存命中] {cacheKey}（5 分钟内不重复请求）",
                port: EnumLogPort.Client, module: EnumLogModule.Net);
            MainWindowViewModel.Instance?.AppMessage =
                string.Format(Languages.Languages.Text_Api_CacheHitFormat, operationName);
            return cachedResult;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, $"读取 API 缓存失败: {cacheKey}");
            ApiCacheService.Invalidate(cacheKey);
            return null;
        }
    }

    /// <summary>
    ///     写入统一缓存（26.4）。<paramref name="success" /> 为 false 时不写入，
    ///     避免把失败结果固化 5 分钟。
    /// </summary>
    private static void CacheIfSuccess(string cacheKey, string? content, bool success)
    {
        if (success && !string.IsNullOrEmpty(content))
        {
            ApiCacheService.SetContent(cacheKey, content);
        }
    }

    /// <summary>
    ///     发送反馈请求
    /// </summary>
    /// <param name="mail">用户邮箱</param>
    /// <param name="feedback">反馈内容</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static async Task<FeedbackResponse> SendFeedBackAsync(string mail, string feedback)
    {
        App.CurrentLogger?.Log("正在发送反馈请求", port: EnumLogPort.Client, module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);

        var body = JsonSerializer.Serialize(new FeedbackBody
        {
            User = mail,
            Comment = feedback,
            Time = DateTime.Now.ToString("O")
        }, App.AppJsonSerializerContext.FeedbackBody);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("feedback");
        var response = await client.ExecuteAsync(request);

        App.CurrentLogger?.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        
        if (string.IsNullOrEmpty(response.Content))
        {
            return new FeedbackResponse { Success = false, Message = "Empty Response" };
        }

        var res = JsonSerializer.Deserialize<FeedbackResponse>(response.Content,
            App.AppJsonSerializerContext.FeedbackResponse);
        return res ?? new FeedbackResponse { Success = false, Message = "Deserialize Error" };
    }

    public static async Task<LocationNameInfo[]?> GetLocationNameAsync(LocationCoordinate locationCoordinate)
    {
        var request = CreateRequest();
        App.CurrentLogger?.LogDebug($"GET Location", port: EnumLogPort.Server,
            module: EnumLogModule.Custom, customModuleName: "API");

        var endpoint = "https://weatherapi.market.xiaomi.com/wtr-v3/location/city/geo?" +
                       $"longitude={locationCoordinate.Longitude}" +
                       $"&latitude={locationCoordinate.Latitude}&locale=zh_cn";
        using var client = new RestClient(new RestClientOptions(endpoint)
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UserAgent = OperatingSystem.IsAndroid() ? "RYCB-PML2/Android 0.0.2" : $"RYCB-PML2/Desktop {App.Version} ",
            Timeout = TimeSpan.FromSeconds(10)
        });

        var response = await client.ExecuteAsync(request);

        if (string.IsNullOrEmpty(response.Content))
        {
            return null;
        }

        var result =
            JsonSerializer.Deserialize<LocationNameInfo[]>(response.Content,
                App.AppJsonSerializerContext.LocationNameInfoArray);
        return result;
    }

    /// <summary>
    ///     发送邮箱
    /// </summary>
    /// <param name="mode">发送模式，目前有: html, vcode, warn</param>
    /// <param name="receiver">发送对象</param>
    /// <param name="sender">发送者</param>
    /// <param name="mailBody">邮件体</param>
    /// <param name="subject">邮件主题</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static async Task<FeedbackResponse> SendEmailAsync(string mode, string receiver, string mailBody,
        string subject, string sender = "noreply")
    {
        App.CurrentLogger?.Log("正在发送邮箱", port: EnumLogPort.Client, module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);

        var body = JsonSerializer.Serialize(new EmailBody
        {
            Mode = mode,
            Receiver = receiver,
            Body = mailBody,
            Sender = sender,
            Subject = subject
        }, App.AppJsonSerializerContext.EmailBody);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("send_email");
        var response = await client.ExecuteAsync(request);

        App.CurrentLogger?.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (!response.IsSuccessful || !response.IsSuccessStatusCode || response.Content is null)
        {
            App.CurrentLogger?.Log(response.Content, EnumLogType.Warn, module: EnumLogModule.Net);
            return new FeedbackResponse
            {
                Success = false,
                Message = Languages.Languages.Text_Api_SendFailed
            };
        }

        var res = JsonSerializer.Deserialize<FeedbackResponse>(response.Content,
            App.AppJsonSerializerContext.FeedbackResponse);
        return res ?? new FeedbackResponse { Success = false, Message = "Deserialize Error" };
    }

    /// <summary>
    ///     获取最新正式版本信息（26.4：走统一 5 分钟缓存）。
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过缓存、强制重新请求（用户点「检查更新」时使用）</param>
    public static async Task<SingleVersionInfo> GetLatestVersionInfoAsync(bool forceRefresh = false)
    {
        // 26.4：缓存优先。更新页 / 主页推荐 / 启动检查共用同一份结果，5 分钟内不重复请求
        if (!forceRefresh &&
            TryGetCached(ApiCacheKeys.LatestVersion, Languages.Languages.Text_Api_OpLatestVersion,
                json => JsonSerializer.Deserialize<SingleVersionInfo>(json,
                    App.AppJsonSerializerContext.SingleVersionInfo)) is { } cachedVersion)
        {
            return cachedVersion;
        }

        App.CurrentLogger?.LogDebug($"GET {BaseApiUrl + "changelog/latest"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger?.Log("正在获取最新版本", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = Languages.Languages.Text_Api_FetchingLatestVersion;

        using var client = CreateClient("changelog/latest");

        var response = await client.ExecuteAsync(CreateRequest());
        App.CurrentLogger?.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        if (string.IsNullOrEmpty(response.Content))
        {
            var fallBack = new SingleVersionInfo
            {
                Success = false,
                Version = "0.0.0",
                Data = null
            };
            return fallBack;
        }

        var result =
            JsonSerializer.Deserialize<SingleVersionInfo>(response.Content,
                App.AppJsonSerializerContext.SingleVersionInfo) ?? new SingleVersionInfo
            {
                Success = false,
                Version = "0.0.0",
                Data = null
            };

        // 26.4：仅成功结果进入缓存
        CacheIfSuccess(ApiCacheKeys.LatestVersion, response.Content, result.Success);

        MainWindowViewModel.Instance?.AppMessage =
            string.Format(Languages.Languages.Text_Api_DoneCodeFormat, (int)response.StatusCode);
        return result;
    }

    /// <summary>
    ///     获取最新预览版本信息（26.4：走统一 5 分钟缓存）。
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过缓存、强制重新请求</param>
    public static async Task<SingleVersionInfo> GetLatestPreviewVersionInfoAsync(bool forceRefresh = false)
    {
        // 26.4：缓存优先，与正式版本共用同一套 5 分钟窗口
        if (!forceRefresh &&
            TryGetCached(ApiCacheKeys.LatestPreviewVersion, Languages.Languages.Text_Api_OpLatestVersion,
                json => JsonSerializer.Deserialize<SingleVersionInfo>(json,
                    App.AppJsonSerializerContext.SingleVersionInfo)) is { } cachedPreviewVersion)
        {
            return cachedPreviewVersion;
        }

        App.CurrentLogger?.LogDebug($"GET {BaseApiUrl + "changelog/preview/latest"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger?.Log("正在获取最新版本", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = Languages.Languages.Text_Api_FetchingLatestVersion;

        using var client = CreateClient("changelog/preview/latest");

        var response = await client.ExecuteAsync(CreateRequest());
        App.CurrentLogger?.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        if (string.IsNullOrEmpty(response.Content))
        {
            var fallBack = new SingleVersionInfo
            {
                Success = false,
                Version = "0.0.0",
                Data = null
            };
            return fallBack;
        }

        var result =
            JsonSerializer.Deserialize<SingleVersionInfo>(response.Content,
                App.AppJsonSerializerContext.SingleVersionInfo) ?? new SingleVersionInfo
            {
                Success = false,
                Version = "0.0.0",
                Data = null
            };

        // 26.4：仅成功结果进入缓存
        CacheIfSuccess(ApiCacheKeys.LatestPreviewVersion, response.Content, result.Success);

        MainWindowViewModel.Instance?.AppMessage =
            string.Format(Languages.Languages.Text_Api_DoneCodeFormat, (int)response.StatusCode);
        return result;
    }

    public static async Task<TunnelErrorInfosShell?> GetTunnelErrorInfoAsync()
    {
        App.CurrentLogger?.LogDebug($"GET {BaseApiUrl + "tpca/errors"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger?.Log("正在获取错误信息", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = Languages.Languages.Text_Api_FetchingErrorInfo;
        using var client = CreateClient("tpca/errors");
        var res = await client.ExecuteAsync(CreateRequest());
        App.CurrentLogger?.Log($"状态: {res.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (res.StatusCode != HttpStatusCode.OK || res.Content is null)
        {
            return new TunnelErrorInfosShell
            {
                Success = false,
                Data = null,
                Count = 0,
                Timestamp = DateTimeOffset.Now.ToString("O")
            };
        }

        var result =
            JsonSerializer.Deserialize<TunnelErrorInfosShell>(res.Content,
                App.AppJsonSerializerContext.TunnelErrorInfosShell);
        return result;
    }

    public static async Task<SingleApiInfo<TunnelErrorInfo>?> GetTunnelErrorInfoAsync(string flag)
    {
        App.CurrentLogger?.LogDebug($"GET {BaseApiUrl + $"tpca/errors/{flag}"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger?.Log("正在获取错误信息", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = Languages.Languages.Text_Api_FetchingErrorInfo;
        using var client = CreateClient($"tpca/errors/{flag}");
        var res = await client.ExecuteAsync(CreateRequest());
        App.CurrentLogger?.Log($"状态: {res.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (res.StatusCode != HttpStatusCode.OK || res.Content is null)
        {
            return new SingleApiInfo<TunnelErrorInfo>
            {
                Success = false,
                Data = null,
                Count = 0,
                Timestamp = DateTimeOffset.Now.ToString("O")
            };
        }

        var result = JsonSerializer.Deserialize<SingleApiInfo<TunnelErrorInfo>>(res.Content,
            App.AppJsonSerializerContext.SingleApiInfoTunnelErrorInfo);

        // 26.4：隧道错误信息按 flag 缓存 5 分钟（终端错误提示反复读取，避免重复请求）
        var errorCacheKey = $"{ApiCacheKeys.TunnelErrorPrefix}{flag}";
        if (result?.Success == true)
        {
            ApiCacheService.SetContent(errorCacheKey, res.Content);
        }
        else
        {
            ApiCacheService.Invalidate(errorCacheKey);
        }

        return result;
    }

    /// <summary>
    ///     获取软件公告列表（26.4：走统一 5 分钟缓存）。
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过缓存、强制重新请求</param>
    public static async Task<SingleApiInfo<NoticeContent[]>> GetAllNoticeAsync(bool forceRefresh = false)
    {
        if (!forceRefresh &&
            TryGetCached(ApiCacheKeys.SoftwareNotice, Languages.Languages.Text_Api_OpSoftwareNotice,
                json => JsonSerializer.Deserialize<SingleApiInfo<NoticeContent[]>>(json,
                    App.AppJsonSerializerContext.SingleApiInfoNoticeContentArray)) is { } cachedNotice)
        {
            return cachedNotice;
        }

        App.CurrentLogger?.LogDebug($"GET {BaseApiUrl + "notice"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger?.Log("正在获取软件公告", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = Languages.Languages.Text_Api_FetchingSoftwareNotice;
        using var client = CreateClient("notice");
        var res = await client.ExecuteAsync(CreateRequest());
        App.CurrentLogger?.Log($"状态: {res.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (res.StatusCode != HttpStatusCode.OK || res.Content is null)
        {
            return new SingleApiInfo<NoticeContent[]>
            {
                Success = false,
                Data = null,
                Count = 0,
                Timestamp = DateTimeOffset.Now.ToString("O")
            };
        }

        var result = JsonSerializer.Deserialize<SingleApiInfo<NoticeContent[]>>(res.Content,
            App.AppJsonSerializerContext.SingleApiInfoNoticeContentArray);

        // 26.4：仅成功结果进入缓存
        CacheIfSuccess(ApiCacheKeys.SoftwareNotice, res.Content, result?.Success == true);
        return result ?? new SingleApiInfo<NoticeContent[]> { Success = false, Data = null };
    }


    private static RestClient CreateClient(string endpoint)
    {
        return new RestClient(new RestClientOptions(BaseApiUrl + endpoint)
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UserAgent = OperatingSystem.IsAndroid() ? $"RYCB-PML2/Android 0.0.2" : $"RYCB-PML2/Desktop {App.Version}",
            Timeout = TimeSpan.FromSeconds(10)
        });
    }
}

public class NoticeContent : ReactiveObject
{
    public NoticeContent()
    {
        ShowNoticeCommand = ReactiveCommand.Create(ShowNotice);
    }

    [JsonPropertyName("active")]
    public bool Active
    {
        get;
        set;
    }

    [JsonPropertyName("content")]
    public string ContentOfNotice
    {
        get;
        set;
    }


    [JsonPropertyName("date")]
    public string Date
    {
        get;
        set;
    }

    [JsonPropertyName("id")]
    public int Id
    {
        get;
        set;
    }

    [JsonPropertyName("priority")]
    public int Priority
    {
        get;
        set;
    }

    [JsonPropertyName("summary")]
    public string Summary
    {
        get;
        set;
    }

    [JsonPropertyName("type")]
    public string Type
    {
        get;
        set;
    }

    public ReactiveCommand<Unit, Unit> ShowNoticeCommand
    {
        get;
    }

    public void ShowNotice()
    {
        var cd = new ContentDialog
        {
            Content = new NoticeView(this, ContentOfNotice),
            Title = Summary,
            PrimaryButtonText = Languages.Languages.Text_Global_Confirm,
            CloseButtonText = Languages.Languages.Text_Global_Close,
            DefaultButton = ContentDialogButton.Primary
        };
        cd.ShowAsync();
    }
}

public record TunnelErrorInfosShell
{
    [JsonPropertyName("count")]
    public int Count
    {
        get;
        set;
    }

    [JsonPropertyName("data")]
    public TunnelErrorInfo[]? Data
    {
        get;
        set;
    }

    [JsonPropertyName("success")]
    public bool Success
    {
        get;
        set;
    }

    [JsonPropertyName("timestamp")]
    public string Timestamp
    {
        get;
        set;
    }
}

public record SingleApiInfo<T>
{
    [JsonPropertyName("count")]
    public int Count
    {
        get;
        set;
    }

    [JsonPropertyName("data")]
    public T? Data
    {
        get;
        set;
    }

    [JsonPropertyName("success")]
    public bool Success
    {
        get;
        set;
    }

    [JsonPropertyName("timestamp")]
    public string Timestamp
    {
        get;
        set;
    }
}

public record TunnelErrorInfo
{
    [JsonPropertyName("flag")]
    public string Flag
    {
        get;
        set;
    }

    [JsonPropertyName("info")]
    public string Info
    {
        get;
        set;
    }

    [JsonPropertyName("solution")]
    public string[]? Solution
    {
        get;
        set;
    }
}

public class EmailBody
{
    [JsonPropertyName("receiver")]
    public string Receiver
    {
        get;
        set;
    }

    [JsonPropertyName("sender")]
    public string Sender
    {
        get;
        set;
    }

    [JsonPropertyName("subject")]
    public string Subject
    {
        get;
        set;
    }

    [JsonPropertyName("body")]
    public string Body
    {
        get;
        set;
    }

    [JsonPropertyName("mode")]
    public string Mode
    {
        get;
        set;
    }
}

public class SingleVersionInfo
{
    [JsonPropertyName("data")]
    public VersionInfo? Data
    {
        get;
        set;
    }

    [JsonPropertyName("success")]
    public bool Success
    {
        get;
        set;
    }

    [JsonPropertyName("version")]
    public string Version
    {
        get;
        set;
    }

    public class VersionInfo
    {
        [JsonPropertyName("changes")]
        public string[]? Changes
        {
            get;
            set;
        }

        [JsonPropertyName("codename")]
        public string Codename
        {
            get;
            set;
        }

        [JsonPropertyName("date")]
        public string Date
        {
            get;
            set;
        }

        [JsonPropertyName("description")]
        public string Description
        {
            get;
            set;
        }
    }
}

public class FeedbackBody
{
    [JsonPropertyName("user")]
    public string User
    {
        get;
        set;
    }

    [JsonPropertyName("comment")]
    public string Comment
    {
        get;
        set;
    }

    [JsonPropertyName("time")]
    public string Time
    {
        get;
        set;
    }
}

public class FeedbackResponse
{
    [JsonPropertyName("id")]
    public int Id
    {
        get;
        set;
    }

    [JsonPropertyName("message")]
    public string Message
    {
        get;
        set;
    }

    [JsonPropertyName("success")]
    public bool Success
    {
        get;
        set;
    }
}