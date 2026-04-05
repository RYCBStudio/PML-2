using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Windowing;
using Message.Avalonia;

namespace MEFrpLauncherX.Core;

public class App : IDisposable
{
    public const string Version = "2.3.0-rc2";

    public const string MEFrpVersion = "0.67.0_20260302_f1907e56";
    public static string Flag = "Desktop";
    public static string ReleaseFlag = "Preview";
    public static readonly string StartupPath = AppDomain.CurrentDomain.BaseDirectory;

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

    public static WindowNotificationManager? WindowNotificationManager;

    public static MessageManager PML2MsgMnger = new()
    {
        HostId = "PML2_Msg",
        Duration = TimeSpan.FromSeconds(3)
    };

    public static LogUtil? CurrentLogger
    {
        get;
        private set;
    }

    public static void Initialize()
    {
        Directory.CreateDirectory(Path.Combine(StartupPath, "Cache"));
        Directory.CreateDirectory(Path.Combine(StartupPath, "Config", "frp"));
        
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
        
        ConfigManager.Initialize();
        
        var i18nPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "i18n");
        Directory.CreateDirectory(i18nPath);
        File.WriteAllText(Path.Combine(i18nPath, "MarkdownAIRender.en-US.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Localization language="English" description="English" cultureName="en-US">
              <MarkdownRender>
                <CopyButtonContent>Copy</CopyButtonContent>
                <CopyNotificationTitle>Copy succeeded</CopyNotificationTitle>
                <CopyNotificationMessage>Copy succeeded</CopyNotificationMessage>
              </MarkdownRender>
            </Localization>
            """);
        File.WriteAllText(Path.Combine(i18nPath, "MarkdownAIRender.zh-CN.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Localization language="Chinese (Simplified)" description="中文简体" cultureName="zh-CN">
              <MarkdownRender>
                <CopyButtonContent>复制</CopyButtonContent>
                <CopyNotificationTitle>复制成功</CopyNotificationTitle>
                <CopyNotificationMessage>复制成功</CopyNotificationMessage>
              </MarkdownRender>
            </Localization>
            """);
        File.WriteAllText(Path.Combine(i18nPath, "MarkdownAIRender.zh-Hant.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Localization language="Chinese (Traditional)" description="中文繁體" cultureName="zh-Hant">
              <MarkdownRender>
                <CopyButtonContent>複製</CopyButtonContent>
                <CopyNotificationTitle>複製成功</CopyNotificationTitle>
                <CopyNotificationMessage>複製成功</CopyNotificationMessage>
              </MarkdownRender>
            </Localization>
            """);
        File.WriteAllText(Path.Combine(i18nPath, "MarkdownAIRender.ja-JP.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Localization language="Japanese" description="日本語" cultureName="ja-JP">
              <MarkdownRender>
                <CopyButtonContent>コピー</CopyButtonContent>
                <CopyNotificationTitle>コピーに成功しました</CopyNotificationTitle>
                <CopyNotificationMessage>コピーに成功しました</CopyNotificationMessage>
              </MarkdownRender>
            </Localization>
            """);
    }

    public void Dispose()
    {
        CurrentLogger?.Dispose();
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
        /// 判断字符串是否为空或null
        /// </summary>
        /// <returns>是否为空或null.</returns>
        public bool IsNullOrEmpty()
        {
            return string.IsNullOrEmpty(str);
        }

        /// <summary>
        /// 将字符串的指定部分大写
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
        /// 将字符串的指定部分小写
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
        /// 判断字符串是否以指定的后缀结尾
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