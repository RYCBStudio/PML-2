using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Windowing;
using MEFrpLauncherX.Core.Services;
using Message.Avalonia;
using Notify.NET.Abstractions;
using Notify.NET.Extensions;

namespace MEFrpLauncherX.Core;

public class App : IDisposable
{
    public const string Version = "26.3.1";
    public const string MEFrpVersion = "0.67.1_20260626_af59eefd";

    public static string Flag = "Desktop";
#if AOT
    public const string ReleaseFlag = "AOT";
#endif
#if !AOT
    public const string ReleaseFlag = "Common";
#endif
    public static readonly string StartupPath = AppDomain.CurrentDomain.BaseDirectory;

    public static AppJsonSerializerContext? AppJsonSerializerContext;

    public static string? SelectedTheme
    {
        get;
        set;
    }

    public static MessageManager PML2MsgMnger = new()
    {
        HostId = "PML2_Msg",
        Duration = TimeSpan.FromSeconds(3)
    };

    public static AppWindow? MainWindow
    {
        get;
        set;
    }

    public static INotificationService NotificationService
    {
        get;
        set;
    }

    public static Visual MainVisual
    {
        get;
        set;
    }

    public static IStorageProvider StorageProvider
    {
        get;
        set;
    }

    public static LogUtil? CurrentLogger
    {
        get;
        private set;
    }

    /// <summary>
    ///     节点延迟/连通性探测服务（全局单例）
    /// </summary>
    public static INodeProbeService NodeProbeService
    {
        get;
        private set;
    }

    public void Dispose() => CurrentLogger?.Dispose();

    public static async Task Initialize(bool externalUse = false)
    {
        if (!externalUse)
        {
            Directory.CreateDirectory(Path.Combine(StartupPath, "Cache"));
            Directory.CreateDirectory(Path.Combine(StartupPath, "Config", "frp"));
        }

        AppJsonSerializerContext = new AppJsonSerializerContext(new JsonSerializerOptions()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        });

        NodeProbeService = new NodeProbeService();

        // 使用 Path.Combine 处理跨平台路径
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        // 确保目录存在
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // 使用 Path.Combine 和正确的日期格式
        var logPath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");

        CurrentLogger = new LogUtil(logPath);
        CurrentLogger.Log("Core App init");
        CurrentLogger.Log("Current OS: " + Environment.OSVersion.Platform);

        if (!externalUse)
        {
            ConfigManager.Initialize();
            if (!Directory.Exists(Path.Combine(StartupPath, "Config", "Themes")))
            {
                Directory.CreateDirectory(Path.Combine(StartupPath, "Config", "Themes"));
            }

            // 26.3.1 S3：通知服务必须在首个 await（SelectedTheme 文件读取 / RYCBApiConverter 网络初始化）之前创建：
            // App.axaml.cs 对 Initialize() 是 fire-and-forget 调用（未 await），主窗口会先于本方法完成而显示；
            // 若隧道在其后、此赋值前即失败，OnTerminalOutputAsync 访问未赋值的 NotificationService 会抛 NRE。
            // 创建失败仅记日志，各消费点需自行判空。
            try
            {
                NotificationService = ServiceCollectionExtensions.CreateNotificationService(opts =>
                {
                    opts.AppName = "PML 2";
                    opts.AppUserModelId = "tech.rycb.pml2";
                });
            }
            catch (Exception ex)
            {
                CurrentLogger?.Error(ex, "初始化系统通知服务失败");
            }

            SelectedTheme = File.Exists(Path.Combine(StartupPath, "Config", "Themes", "selected"))
                ? (await File.ReadAllTextAsync(Path.Combine(StartupPath, "Config", "Themes", "selected"))).Trim()
                : null;
            await RYCBApiConverter.InitializeAsync();
        }
    }
}
