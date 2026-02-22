using System.Net;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core.Controls;
using Newtonsoft.Json;
using RestSharp;

namespace MEFrpLauncherX.Core;

public class RYCBApiConverter
{
    public const string BaseApiUrl = "https://api.rycb.mxj.pub/api/";

    public static RestClient? CurrentClient
    {
        get;
        set;
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
    /// 发送反馈请求
    /// </summary>
    /// <param name="mail">用户邮箱</param>
    /// <param name="feedback">反馈内容</param>
    /// <returns>(是否成功, 返回的response内容)</returns>
    public static async Task<FeedbackResponse> SendFeedBackAsync(string mail, string feedback)
    {
        App.CurrentLogger.Log("正在发送反馈请求", port: EnumLogPort.Client, module: EnumLogModule.Net);

        var request = CreateRequest(Method.Post);

        var body = JsonConvert.SerializeObject(new FeedbackBody
        {
            user = mail,
            comment = feedback,
            time = DateTime.Now.ToString("O"),
        }, Formatting.Indented);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("feedback");
        var response = await client.ExecuteAsync(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        var res = JsonConvert.DeserializeObject<FeedbackResponse>(response.Content);
        return res;
    }

    /// <summary>
    /// 发送邮箱
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

        var body = JsonConvert.SerializeObject(new EmailBody
        {
            mode = mode,
            receiver = receiver,
            body = mailBody,
            subject = subject
        }, Formatting.Indented);

        request.AddParameter("application/json", body, ParameterType.RequestBody);

        using var client = CreateClient("send_email");
        var response = await client.ExecuteAsync(request);

        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        if (!response.IsSuccessful || !response.IsSuccessStatusCode)
        {
            App.CurrentLogger.Log(response.Content, EnumLogType.Warn, module: EnumLogModule.Net);
        }

        var res = JsonConvert.DeserializeObject<FeedbackResponse>(response.Content);
        return res;
    }

    public static async Task<SingleVersionInfo> GetLatestVersionInfoAsync()
    {
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + "changelog/latest"}", port: EnumLogPort.Server,
            module: EnumLogModule.Custom, customModuleName: "API");
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

        var result = JsonConvert.DeserializeObject<SingleVersionInfo>(response.Content) ?? new SingleVersionInfo
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
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + "changelog/preview/latest"}", port: EnumLogPort.Server,
            module: EnumLogModule.Custom, customModuleName: "API");
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

        var result = JsonConvert.DeserializeObject<SingleVersionInfo>(response.Content) ?? new SingleVersionInfo
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
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + "tpca/errors"}", port: EnumLogPort.Server,
            module: EnumLogModule.Custom, customModuleName: "API");
        App.CurrentLogger.Log("正在获取错误信息", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = "正在获取错误信息";
        using var client = CreateClient("tpca/errors");
        var res = await client.ExecuteAsync(CreateRequest(withAuthorization: false));
        App.CurrentLogger.Log($"状态: {res.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        var result = JsonConvert.DeserializeObject<TunnelErrorInfosShell>(res.Content);
        return result;
    }

    public static async Task<SingleApiInfo<TunnelErrorInfo>?> GetTunnelErrorInfoAsync(string flag)
    {
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + $"tpca/errors/{flag}"}", port: EnumLogPort.Server,
            module: EnumLogModule.Custom, customModuleName: "API");
        App.CurrentLogger.Log("正在获取错误信息", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = "正在获取错误信息";
        using var client = CreateClient($"tpca/errors/{flag}");
        var res = await client.ExecuteAsync(CreateRequest(withAuthorization: false));
        App.CurrentLogger.Log($"状态: {res.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        var result = JsonConvert.DeserializeObject<SingleApiInfo<TunnelErrorInfo>>(res.Content);
        return result;
    }

    public static async Task<SingleApiInfo<NoticeContent[]>> GetAllNoticeAsync()
    {
        App.CurrentLogger.LogDebug($"GET {BaseApiUrl + "notice"}", port: EnumLogPort.Server,
            module: EnumLogModule.Custom, customModuleName: "API");
        App.CurrentLogger.Log("正在获取软件公告", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = "正在获取软件公告";
        using var client = CreateClient("notice");
        var res = await client.ExecuteAsync(CreateRequest(withAuthorization: false));
        App.CurrentLogger.Log($"状态: {res.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);
        var result = JsonConvert.DeserializeObject<SingleApiInfo<NoticeContent[]>>(res.Content);
        return result;
    }


    private static RestClient CreateClient(string endpoint)
    {
        return new RestClient(new RestClientOptions(BaseApiUrl + endpoint)
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UserAgent = OperatingSystem.IsAndroid() ? "RYCB-PML2/Android 0.0.2" : "RYCB-PML2/Desktop 2.1.0",
            Timeout = TimeSpan.FromSeconds(10),
        });
    }
}

public class NoticeContent(
    bool active,
    string content,
    string date,
    int id,
    int priority,
    string summary,
    string type
)
{
    public bool Active
    {
        get;
        set;
    } = active;

    public string ContentOfNotice
    {
        get;
        set;
    } = content;
    

    public string Date
    {
        get;
        set;
    } = date;

    public int Id
    {
        get;
        set;
    } = id;

    public int Priority
    {
        get;
        set;
    } = priority;

    public string Summary
    {
        get;
        set;
    } = summary;

    public string Type
    {
        get;
        set;
    } = type;

    public void ShowNotice()
    {
        var cd = new ContentDialog()
        {
            Content = new NoticeView(this, ContentOfNotice),
            Title = summary,
            PrimaryButtonText = "确定",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary,
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