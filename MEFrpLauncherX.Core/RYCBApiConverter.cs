using System.Net;
using System.Reactive;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core.Controls;
using ReactiveUI;
using RestSharp;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。
#pragma warning disable CS8603 // 可能返回 null 引用。

namespace MEFrpLauncherX.Core;

public class RYCBApiConverter
{
    public static string BaseApiUrl = "https://api.rycb.mxj.pub/api/";

    public static RestClient? CurrentClient
    {
        get;
        set;
    }

    public static async Task<bool> InitializeAsync()
    {
            App.CurrentLogger.Log("正在初始化API客户端", port: EnumLogPort.Client, module: EnumLogModule.Net);
            CurrentClient = CreateClient("api/health");
            var res = await CurrentClient.ExecuteAsync(new RestRequest { Method = Method.Options });
            if (!res.IsSuccessful)
            {
                App.CurrentLogger.Log("API服务器未启动", port: EnumLogPort.Server, module: EnumLogModule.Net);
                BaseApiUrl = "https://api.rycb.tech/api/";
                CurrentClient = CreateClient("api/health");
                res = await CurrentClient.ExecuteAsync(new RestRequest { Method = Method.Options });
                CurrentClient.Dispose();
                if (!res.IsSuccessful)
                {
                    App.CurrentLogger.Log("API服务器未启动", port: EnumLogPort.Server, module: EnumLogModule.Net);
                    return false;
                }
            }
            CurrentClient.Dispose();
            App.CurrentLogger.Log("API客户端初始化完成", port: EnumLogPort.Client, module: EnumLogModule.Net);
            return true;
    }

    private static RestRequest CreateRequest(Method method = Method.Get, bool withAuthorization = true)
    {
        var request = new RestRequest { Method = method };
        if (method != Method.Get)
        {
            request.AddHeader("Content-Type", "application/json");
        }

        return request;
    }

    /// <summary>
    ///     发送反馈请求
    /// </summary>
    /// <param name="mail">用户邮箱</param>
    /// <param name="feedback">反馈内容</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static async Task<FeedbackResponse> SendFeedBackAsync(string mail, string feedback)
    {
        App.CurrentLogger.Log("正在发送反馈请求", port: EnumLogPort.Client, module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);

        var body = JsonSerializer.Serialize(new FeedbackBody
        {
            user = mail,
            comment = feedback,
            time = DateTime.Now.ToString("O")
        }, App.AppJsonSerializerContext.FeedbackBody);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("feedback");
        var response = await client.ExecuteAsync(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        var res = JsonSerializer.Deserialize<FeedbackResponse>(response.Content,
            App.AppJsonSerializerContext.FeedbackResponse);
        return res;
    }

    /// <summary>
    ///     发送邮箱
    /// </summary>
    /// <param name="mode">发送模式，目前有: html, vcode, warn</param>
    /// <param name="receiver">发送对象</param>
    /// <param name="mailBody">邮件体</param>
    /// <param name="subject">邮件主题</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static async Task<FeedbackResponse> SendEmailAsync(string mode, string receiver, string mailBody,
        string subject)
    {
        App.CurrentLogger.Log("正在发送邮箱", port: EnumLogPort.Client, module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);

        var body = JsonSerializer.Serialize(new EmailBody
        {
            mode = mode,
            receiver = receiver,
            body = mailBody,
            subject = subject
        }, App.AppJsonSerializerContext.EmailBody);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("send_email");
        var response = await client.ExecuteAsync(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (!response.IsSuccessful || !response.IsSuccessStatusCode || response.Content is null)
        {
            App.CurrentLogger.Log(response.Content, EnumLogType.Warn, module: EnumLogModule.Net);
            return new FeedbackResponse
            {
                success = false,
                message = "发送失败"
            };
        }

        var res = JsonSerializer.Deserialize<FeedbackResponse>(response.Content,
            App.AppJsonSerializerContext.FeedbackResponse);
        return res;
    }

    public static async Task<SingleVersionInfo> GetLatestVersionInfoAsync()
    {
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + "changelog/latest"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger.Log("正在获取最新版本", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = "正在获取最新版本";

        using var client = CreateClient("changelog/latest");

        var response = await client.ExecuteAsync(CreateRequest(withAuthorization: false));
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        if (string.IsNullOrEmpty(response.Content))
        {
            var fallBack = new SingleVersionInfo
            {
                success = false,
                version = "0.0.0",
                data = default
            };
            return fallBack;
        }

        var result =
            JsonSerializer.Deserialize<SingleVersionInfo>(response.Content,
                App.AppJsonSerializerContext.SingleVersionInfo) ?? new SingleVersionInfo
            {
                success = false,
                version = "0.0.0",
                data = default
            };

        MainWindowViewModel.Instance?.AppMessage = $"完成, 返回代码: {(int)response.StatusCode}";
        return result;
    }

    public static async Task<SingleVersionInfo> GetLatestPreviewVersionInfoAsync()
    {
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + "changelog/preview/latest"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger.Log("正在获取最新版本", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = "正在获取最新版本";

        using var client = CreateClient("changelog/preview/latest");

        var response = await client.ExecuteAsync(CreateRequest(withAuthorization: false));
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        if (string.IsNullOrEmpty(response.Content))
        {
            var fallBack = new SingleVersionInfo
            {
                success = false,
                version = "0.0.0",
                data = default
            };
            return fallBack;
        }

        var result =
            JsonSerializer.Deserialize<SingleVersionInfo>(response.Content,
                App.AppJsonSerializerContext.SingleVersionInfo) ?? new SingleVersionInfo
            {
                success = false,
                version = "0.0.0",
                data = default
            };

        MainWindowViewModel.Instance?.AppMessage = $"完成, 返回代码: {(int)response.StatusCode}";
        return result;
    }

    public static async Task<TunnelErrorInfosShell?> GetTunnelErrorInfoAsync()
    {
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + "tpca/errors"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger.Log("正在获取错误信息", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = "正在获取错误信息";
        using var client = CreateClient("tpca/errors");
        var res = await client.ExecuteAsync(CreateRequest(withAuthorization: false));
        App.CurrentLogger.Log($"状态: {res.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        var result =
            JsonSerializer.Deserialize<TunnelErrorInfosShell>(res.Content,
                App.AppJsonSerializerContext.TunnelErrorInfosShell);
        return result;
    }

    public static async Task<SingleApiInfo<TunnelErrorInfo>?> GetTunnelErrorInfoAsync(string flag)
    {
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + $"tpca/errors/{flag}"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger.Log("正在获取错误信息", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = "正在获取错误信息";
        using var client = CreateClient($"tpca/errors/{flag}");
        var res = await client.ExecuteAsync(CreateRequest(withAuthorization: false));
        App.CurrentLogger.Log($"状态: {res.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (res.StatusCode != HttpStatusCode.OK || res.Content is null)
        {
            return new SingleApiInfo<TunnelErrorInfo>
            {
                success = false,
                data = default,
                count = 0,
                timestamp = DateTimeOffset.Now.ToString("O")
            };
            ;
        }

        var result = JsonSerializer.Deserialize<SingleApiInfo<TunnelErrorInfo>>(res.Content,
            App.AppJsonSerializerContext.SingleApiInfoTunnelErrorInfo);
        return result;
    }

    public static async Task<SingleApiInfo<NoticeContent[]>> GetAllNoticeAsync()
    {
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + "notice"}", EnumLogPort.Server,
            EnumLogModule.Custom, "API");
        App.CurrentLogger.Log("正在获取软件公告", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = "正在获取软件公告";
        using var client = CreateClient("notice");
        var res = await client.ExecuteAsync(CreateRequest(withAuthorization: false));
        App.CurrentLogger.Log($"状态: {res.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (res.StatusCode != HttpStatusCode.OK || res.Content is null)
        {
            return new SingleApiInfo<NoticeContent[]>
            {
                success = false,
                data = default,
                count = 0,
                timestamp = DateTimeOffset.Now.ToString("O")
            };
            ;
        }

        var result = JsonSerializer.Deserialize<SingleApiInfo<NoticeContent[]>>(res.Content,
            App.AppJsonSerializerContext.SingleApiInfoNoticeContentArray);
        return result;
    }


    private static RestClient CreateClient(string endpoint)
    {
        return new RestClient(new RestClientOptions(BaseApiUrl + endpoint)
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UserAgent = OperatingSystem.IsAndroid() ? "RYCB-PML2/Android 0.0.2" : "RYCB-PML2/Desktop 2.1.0",
            Timeout = TimeSpan.FromSeconds(10)
        });
    }
}

public class NoticeContent:ReactiveObject
{
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


    public ReactiveCommand<Unit, Unit> ShowNoticeCommand => ReactiveCommand.Create(ShowNotice);
    
    public void ShowNotice()
    {
        var cd = new ContentDialog
        {
            Content = new NoticeView(this, ContentOfNotice),
            Title = Summary,
            PrimaryButtonText = "确定",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary
        };
        cd.ShowAsync();
    }
}

public record TunnelErrorInfosShell
{
    public int count
    {
        get;
        set;
    }

    public TunnelErrorInfo[] data
    {
        get;
        set;
    }

    public bool success
    {
        get;
        set;
    }

    public string timestamp
    {
        get;
        set;
    }
}

public record SingleApiInfo<T>
{
    public int count
    {
        get;
        set;
    }

    public T data
    {
        get;
        set;
    }

    public bool success
    {
        get;
        set;
    }

    public string timestamp
    {
        get;
        set;
    }
}

public record TunnelErrorInfo
{
    public string Flag
    {
        get;
        set;
    }

    public string Info
    {
        get;
        set;
    }

    public string[] Solution
    {
        get;
        set;
    }
}

public class EmailBody
{
    public string receiver
    {
        get;
        set;
    }

    public string subject
    {
        get;
        set;
    }

    public string body
    {
        get;
        set;
    }

    public string mode
    {
        get;
        set;
    }
}

public class SingleVersionInfo
{
    public VersionInfo data
    {
        get;
        set;
    }

    public bool success
    {
        get;
        set;
    }

    public string version
    {
        get;
        set;
    }

    public class VersionInfo
    {
        public string[] changes
        {
            get;
            set;
        }

        public string codename
        {
            get;
            set;
        }

        public string date
        {
            get;
            set;
        }

        public string description
        {
            get;
            set;
        }
    }
}

public class FeedbackBody
{
    public string user
    {
        get;
        set;
    }

    public string comment
    {
        get;
        set;
    }

    public string time
    {
        get;
        set;
    }
}

public class FeedbackResponse
{
    public int id
    {
        get;
        set;
    }

    public string message
    {
        get;
        set;
    }

    public bool success
    {
        get;
        set;
    }
}