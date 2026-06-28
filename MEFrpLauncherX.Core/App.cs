using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Windowing;
using Message.Avalonia;

namespace MEFrpLauncherX.Core;

public class App : IDisposable
{
    public const string Version = "26.1.0";
    public const string MEFrpVersion = "0.67.1_20260626_af59eefd";

    public static string Flag = "Desktop";
#if AOT
    public const string ReleaseFlag = "AOT";
#endif
#if !AOT
    public const string ReleaseFlag = "Common";
#endif
    public static readonly string StartupPath = AppDomain.CurrentDomain.BaseDirectory;

    internal static AppJsonSerializerContext AppJsonSerializerContext;

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

    public static LogUtil CurrentLogger
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
            SelectedTheme = File.Exists(Path.Combine(StartupPath, "Config", "Themes", "selected"))
                ? (await File.ReadAllTextAsync(Path.Combine(StartupPath, "Config", "Themes", "selected"))).Trim()
                : null;
            await RYCBApiConverter.InitializeAsync();
        }
    }
}

public static class Extensions
{
    public static Process OpenUrl(string url)
    {
        return Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        })!;
    }

    /// <param name="str">待检查的字符串</param>
    extension(string str)
    {
        /// <summary>
        ///     判断字符串是否为空或null
        /// </summary>
        /// <returns>是否为空或null.</returns>
        public bool IsNullOrEmpty() => string.IsNullOrEmpty(str);

        /// <summary>
        ///     将字符串的指定部分大写
        /// </summary>
        /// <param name="startIndex">开始大写的索引</param>
        /// <param name="length">修改的长度</param>
        /// <returns>修改后的字符串</returns>
        public string ToUpper(int startIndex, int length = 1)
        {
            return string.Concat(str.AsSpan(0, startIndex), str.Substring(startIndex, length).ToUpper(),
                str.AsSpan(startIndex + length));
        }

        /// <summary>
        ///     将字符串的指定部分小写
        /// </summary>
        /// <param name="startIndex">开始小写的索引</param>
        /// <param name="length">修改的长度</param>
        /// <returns>修改后的字符串</returns>
        public string ToLower(int startIndex, int length = 1)
        {
            return string.Concat(str.AsSpan(0, startIndex), str.Substring(startIndex, length).ToLower(),
                str.AsSpan(startIndex + length));
        }

        /// <summary>
        ///     判断字符串是否以指定的后缀结尾
        /// </summary>
        /// <param name="suffixes">要判断的后缀, 多个后缀用<c>,</c>分隔</param>
        /// <returns></returns>
        public bool EndsWithEx(string suffixes)
        {
            var possibleSuffix = suffixes.Split(',');
            return possibleSuffix.Any(str.ToLower().EndsWith);
        }
    }
}