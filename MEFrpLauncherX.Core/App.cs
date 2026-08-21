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
    public const string Version = "26.3.0";
    public const string MEFrpVersion = "0.67.1_20260626_af59eefd";

    public static string Flag = "Desktop";
#if AOT
    public const string ReleaseFlag = "AOT";
#endif
#if !AOT
    public const string ReleaseFlag = "Common";
#endif
    public static readonly string StartupPath = AppDomain.CurrentDomain.BaseDirectory;

    public static AppJsonSerializerContext AppJsonSerializerContext;

    public static string? SelectedTheme
    {
        get;
        set;
    }

    public static WindowNotificationManager? WindowNotificationManager;

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

            SelectedTheme = File.Exists(Path.Combine(StartupPath, "Config", "Themes", "selected"))
                ? (await File.ReadAllTextAsync(Path.Combine(StartupPath, "Config", "Themes", "selected"))).Trim()
                : null;
            await RYCBApiConverter.InitializeAsync();
            NotificationService = ServiceCollectionExtensions.CreateNotificationService(opts =>
            {
                opts.AppName = "PML 2";
                opts.AppUserModelId = "tech.rycb.pml2";
            });
        }
    }
}
